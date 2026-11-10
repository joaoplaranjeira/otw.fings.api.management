using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Domain.Enums;
using otw.fings.api.management.DTOs;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Services;

namespace otw.fings.api.management.Tests;

public sealed class InsightsServiceTests
{
    [Fact]
    public async Task GetInsights_ReturnsEverySupportedInsightFromExplainableHistory()
    {
        await using var db = CreateDbContext();
        var (household, food, services, notApplicable) = await SeedAsync(db);
        var supermarket = new Subcategory { CategoryId = food.Id, Name = "Supermercado", Category = food };
        db.Subcategories.Add(supermarket);

        var budget = new BudgetPeriod
        {
            HouseholdId = household.Id,
            Name = "Orçamento",
            StartMonth = new(2026, 10, 1),
            EndMonth = new(2027, 1, 1),
            MonthlyAmount = 400m,
            Status = BudgetStatus.Active
        };
        budget.Allocations.Add(new()
        {
            BudgetPeriodId = budget.Id,
            CategoryId = food.Id,
            Category = food,
            MonthlyAmount = 100m
        });
        budget.Allocations.Add(new()
        {
            BudgetPeriodId = budget.Id,
            CategoryId = services.Id,
            Category = services,
            MonthlyAmount = 50m
        });
        db.BudgetPeriods.Add(budget);
        db.RecurringExpenses.Add(new()
        {
            HouseholdId = household.Id,
            CategoryId = services.Id,
            Description = "Internet",
            MerchantName = "Operador",
            Amount = 50m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new(2026, 1, 1),
            NextOccurrenceDate = new(2027, 2, 1),
            IsActive = true
        });

        foreach (var date in new[] { new DateOnly(2026, 10, 5), new(2026, 11, 5), new(2026, 12, 5) })
        {
            db.Expenses.Add(Expense(household, food, date, 120m, "Compras", "Mercado habitual", supermarket));
        }
        db.Expenses.AddRange(
            Expense(household, food, new(2026, 10, 10), 20m, "Compra", "Mercado Alfa", supermarket),
            Expense(household, food, new(2026, 11, 10), 22m, "Compra", "Mercado Alfa", supermarket),
            Expense(household, food, new(2026, 12, 10), 21m, "Compra", "Mercado Alfa", supermarket),
            Expense(household, services, new(2026, 10, 15), 12m, "Streaming", "StreamCo"),
            Expense(household, services, new(2026, 11, 15), 12m, "Streaming", "StreamCo"),
            Expense(household, services, new(2026, 12, 15), 12m, "Streaming", "StreamCo"),
            Expense(household, food, new(2027, 1, 5), 260m, "Compras do mês", "Supermercado", supermarket),
            Expense(household, food, new(2027, 1, 10), 60m, "Compra grande", "Mercado Alfa", supermarket),
            Expense(household, services, new(2027, 1, 15), 12m, "Streaming", "StreamCo"),
            Expense(household, food, new(2027, 1, 16), 5m, "Café", null, supermarket),
            Expense(household, food, new(2027, 1, 17), 6m, "Lanche", null, supermarket),
            Expense(household, food, new(2027, 1, 18), 7m, "Café", null, supermarket),
            Expense(household, notApplicable, new(2027, 1, 19), 20m, "Por classificar", null));
        await db.SaveChangesAsync();

        var clock = new FixedTimeProvider(new DateTimeOffset(2027, 1, 20, 12, 0, 0, TimeSpan.Zero));
        var service = new InsightsService(new FinanceRepository(db), clock);

        var response = await service.GetInsightsAsync(
            household.Id, 1, new DateOnly(2027, 1, 1), CancellationToken.None);

        Assert.Equal(new DateOnly(2027, 1, 1), response.Month);
        Assert.Equal("EUR", response.Currency);
        Assert.Equal(3, response.HistoryMonths);
        Assert.Equal(Enum.GetValues<InsightType>().Order(), response.Insights.Select(x => x.Type).Order());
        Assert.All(response.Insights, insight =>
        {
            Assert.InRange(insight.Confidence, 0m, 1m);
            Assert.NotEmpty(insight.Metrics);
        });

        var pace = Assert.Single(response.Insights, x => x.Type == InsightType.BudgetPace);
        Assert.Equal(InsightSeverity.Critical, pace.Severity);
        Assert.True(MetricValue(pace, "projected") > 400m);

        var unusual = Assert.Single(response.Insights, x => x.Type == InsightType.UnusualExpense);
        Assert.Contains("Mercado Alfa", unusual.Title);
        Assert.Equal(3, unusual.SampleSize);

        var subscriptions = Assert.Single(response.Insights, x => x.Type == InsightType.PotentialSubscriptions);
        Assert.Equal(2m, MetricValue(subscriptions, "candidateCount"));
        Assert.Contains("StreamCo", subscriptions.Explanation);

        var accuracy = Assert.Single(response.Insights, x => x.Type == InsightType.BudgetAccuracy);
        Assert.Contains("Alimentação", accuracy.Title);
    }

    [Fact]
    public async Task GetInsights_WithLittleHistory_ReturnsOnlySupportedCurrentPeriodFacts()
    {
        await using var db = CreateDbContext();
        var (household, food, _, _) = await SeedAsync(db);
        db.Expenses.Add(Expense(household, food, new DateOnly(2027, 1, 10), 25m, "Compra", "Loja"));
        await db.SaveChangesAsync();
        var service = new InsightsService(
            new FinanceRepository(db),
            new FixedTimeProvider(new DateTimeOffset(2027, 1, 20, 12, 0, 0, TimeSpan.Zero)));

        var response = await service.GetInsightsAsync(
            household.Id, 1, new DateOnly(2027, 1, 1), CancellationToken.None);

        Assert.Equal(0, response.HistoryMonths);
        Assert.DoesNotContain(response.Insights, x => x.Type is
            InsightType.SpendingTrend or
            InsightType.SpendingDriver or
            InsightType.UnusualExpense or
            InsightType.PotentialSubscriptions);
        Assert.Contains(response.Insights, x => x.Type == InsightType.MerchantConcentration);
        Assert.Contains(response.Insights, x => x.Type == InsightType.DataQuality);
    }

    [Fact]
    public async Task GetInsights_RejectsUserOutsideHousehold()
    {
        await using var db = CreateDbContext();
        var (household, _, _, _) = await SeedAsync(db);
        var service = new InsightsService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetInsightsAsync(
            household.Id, 999, new DateOnly(2027, 1, 1), CancellationToken.None));
    }

    private static decimal MetricValue(InsightResponse insight, string key) =>
        Assert.Single(insight.Metrics, x => x.Key == key).Value;

    private static Expense Expense(
        Household household,
        Category category,
        DateOnly date,
        decimal amount,
        string description,
        string? merchant,
        Subcategory? subcategory = null) => new()
    {
        HouseholdId = household.Id,
        Household = household,
        CategoryId = category.Id,
        Category = category,
        SubcategoryId = subcategory?.Id,
        Subcategory = subcategory,
        Date = date,
        Amount = amount,
        Description = description,
        MerchantName = merchant,
        Status = FinancialRecordStatus.Confirmed
    };

    private static FingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FingsDbContext(options);
    }

    private static async Task<(Household Household, Category Food, Category Services, Category NotApplicable)> SeedAsync(FingsDbContext db)
    {
        var user = new User
        {
            Id = 1,
            Name = "Owner",
            Username = "owner",
            Email = "owner@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        var household = new Household { Name = "Família", Currency = "EUR", TimeZone = "Europe/Lisbon" };
        var food = new Category { HouseholdId = household.Id, Name = "Alimentação", Household = household };
        var services = new Category { HouseholdId = household.Id, Name = "Serviços e subscrições", Household = household };
        var notApplicable = new Category { HouseholdId = household.Id, Name = "Não aplicável", Household = household };
        db.AddRange(
            user,
            household,
            new HouseholdMember { HouseholdId = household.Id, UserId = user.Id, Role = HouseholdRole.Owner },
            food,
            services,
            notApplicable);
        await db.SaveChangesAsync();
        return (household, food, services, notApplicable);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
