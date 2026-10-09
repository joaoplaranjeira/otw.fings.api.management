using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Domain.Enums;
using otw.fings.api.management.Services.Notifications;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Tests;

public sealed class NotificationTests
{
    [Fact]
    public void WebPushPayload_UsesCamelCaseTitleAndBody()
    {
        var notification = new Notification
        {
            Title = "Nova despesa",
            Body = "Maria adicionou 12,50 € — Almoço",
            ActionUrl = "/?view=expenses",
            DeduplicationKey = "expense:1"
        };

        using var payload = JsonDocument.Parse(WebPushNotificationChannel.SerializePayload(notification));

        Assert.Equal("Nova despesa", payload.RootElement.GetProperty("title").GetString());
        Assert.Equal("Maria adicionou 12,50 € — Almoço", payload.RootElement.GetProperty("body").GetString());
        Assert.False(payload.RootElement.TryGetProperty("Title", out _));
        Assert.False(payload.RootElement.TryGetProperty("Body", out _));
    }

    [Fact]
    public async Task CreatingExpense_CreatesExactlyOneAtomicOutboxEvent()
    {
        await using var db = CreateContext();
        var householdId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        db.Expenses.Add(NewExpense(householdId, categoryId, 23.40m));

        await db.SaveChangesAsync();

        var events = await db.NotificationEventOutbox.Where(x => x.EventType == "Expense.Created").ToListAsync();
        var item = Assert.Single(events);
        Assert.Equal(householdId, item.HouseholdId);
        Assert.Contains("23.40", item.PayloadJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(76, 82, 80, true)]
    [InlineData(82, 85, 80, false)]
    [InlineData(99, 101, 100, true)]
    public async Task BudgetThresholdEvaluator_OnlyMatchesCrossing(decimal previous, decimal current, decimal threshold, bool expected)
    {
        var rule = new NotificationRule
        {
            EventType = "Budget.UsageChanged",
            ConditionType = "BudgetThresholdCrossed",
            ConditionJson = JsonSerializer.Serialize(new { thresholdPercentage = threshold, direction = "up" })
        };
        using var data = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            budgetId = Guid.NewGuid(), period = "2026-10", categoryId = (Guid?)null,
            previousPercentage = previous, currentPercentage = current
        }));
        var envelope = new NotificationEventEnvelope(Guid.NewGuid(), rule.EventType, 1, Guid.NewGuid(), 1,
            Guid.NewGuid(), "Budget", DateTimeOffset.UtcNow, data);

        var result = await new BudgetThresholdCrossedRuleEvaluator().EvaluateAsync(rule, envelope, default);

        Assert.Equal(expected, result.IsMatch);
    }

    [Fact]
    public async Task BudgetThresholdEvaluator_MatchesFiftyPercentCrossing()
    {
        var rule = new NotificationRule
        {
            EventType = "Budget.UsageChanged",
            ConditionType = "BudgetThresholdCrossed",
            ConditionJson = "{\"thresholdPercentage\":50,\"direction\":\"up\"}"
        };
        using var data = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            budgetId = Guid.NewGuid(), period = "2026-10", categoryId = (Guid?)null,
            previousPercentage = 49, currentPercentage = 50
        }));
        var envelope = new NotificationEventEnvelope(Guid.NewGuid(), rule.EventType, 1, Guid.NewGuid(), 1,
            Guid.NewGuid(), "Budget", DateTimeOffset.UtcNow, data);

        var result = await new BudgetThresholdCrossedRuleEvaluator().EvaluateAsync(rule, envelope, default);

        Assert.True(result.IsMatch);
    }

    [Fact]
    public async Task ExpenseCrossingBudget_CreatesUsageEventWithPreviousAndCurrentValues()
    {
        await using var db = CreateContext();
        var householdId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        db.BudgetPeriods.Add(new BudgetPeriod
        {
            HouseholdId = householdId, Name = "Outubro", StartMonth = new DateOnly(2026, 10, 1),
            EndMonth = new DateOnly(2026, 10, 1), MonthlyAmount = 1000, Status = BudgetStatus.Active
        });
        db.Expenses.Add(NewExpense(householdId, categoryId, 760));
        await db.SaveChangesAsync();
        db.NotificationEventOutbox.RemoveRange(db.NotificationEventOutbox);
        await db.SaveChangesAsync();

        db.Expenses.Add(NewExpense(householdId, categoryId, 60));
        await db.SaveChangesAsync();

        var usage = await db.NotificationEventOutbox.SingleAsync(x => x.EventType == "Budget.UsageChanged");
        using var payload = JsonDocument.Parse(usage.PayloadJson);
        Assert.Equal(760, payload.RootElement.GetProperty("previousSpentAmount").GetDecimal());
        Assert.Equal(820, payload.RootElement.GetProperty("currentSpentAmount").GetDecimal());
        Assert.Equal(76, payload.RootElement.GetProperty("previousPercentage").GetDecimal());
        Assert.Equal(82, payload.RootElement.GetProperty("currentPercentage").GetDecimal());
    }

    [Fact]
    public async Task ProcessingSameEventTwice_DoesNotDuplicateNotification()
    {
        await using var db = CreateContext();
        var household = new Household { Name = "Casa" };
        var actor = new User { Id = 101, Name = "Ana", Username = "ana", Email = "ana@example.test", IsActive = true };
        var recipient = new User { Id = 102, Name = "Bruno", Username = "bruno", Email = "bruno@example.test", IsActive = true };
        db.Users.AddRange(actor, recipient);
        db.Households.Add(household);
        db.HouseholdMembers.AddRange(
            new HouseholdMember { HouseholdId = household.Id, UserId = actor.Id, Role = HouseholdRole.Owner },
            new HouseholdMember { HouseholdId = household.Id, UserId = recipient.Id, Role = HouseholdRole.Member });
        var category = new Category { HouseholdId = household.Id, Name = "Compras" };
        db.Categories.Add(category);
        db.Expenses.Add(NewExpense(household.Id, category.Id, 10, actor.Id));
        await db.SaveChangesAsync();
        var expenseEvent = await db.NotificationEventOutbox.SingleAsync(x => x.EventType == "Expense.Created");

        var processor = CreateProcessor(db);
        await processor.ProcessBatchAsync(default);
        Assert.True(string.IsNullOrEmpty(expenseEvent.LastError), expenseEvent.LastError);
        expenseEvent.ProcessedAtUtc = null;
        expenseEvent.AvailableAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await processor.ProcessBatchAsync(default);

        var notification = Assert.Single(await db.Notifications.Where(x => x.EventId == expenseEvent.Id).ToListAsync());
        Assert.Equal(recipient.Id, notification.UserId);
        Assert.Contains(await db.NotificationDeliveries.ToListAsync(), x => x.Channel == NotificationChannelType.InApp);
    }

    private static FingsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new FingsDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static Expense NewExpense(Guid householdId, Guid categoryId, decimal amount, long actor = 1) => new()
    {
        HouseholdId = householdId, CreatedByUserId = actor, NotificationActorUserId = actor,
        CategoryId = categoryId, Date = new DateOnly(2026, 10, 7), Amount = amount,
        Description = "Compras", Status = FinancialRecordStatus.Confirmed
    };

    private static NotificationEventProcessor CreateProcessor(FingsDbContext db)
    {
        INotificationRuleEvaluator[] evaluators =
        [
            new AlwaysRuleEvaluator(), new BudgetThresholdCrossedRuleEvaluator(), new ExpenseAmountAboveRuleEvaluator(),
            new CategoryExpenseThresholdRuleEvaluator(), new MonthlyExpenseThresholdRuleEvaluator()
        ];
        INotificationRecipientResolver[] resolvers =
        [
            new AllHouseholdMembersResolver(db), new HouseholdMembersExceptActorResolver(db),
            new HouseholdOwnersResolver(db), new HouseholdManagersResolver(db),
            new SpecificUsersResolver(db), new ActorOnlyResolver(db)
        ];
        return new NotificationEventProcessor(db, evaluators, resolvers, new NotificationTemplateRenderer(db),
            Options.Create(new NotificationSettings()), NullLogger<NotificationEventProcessor>.Instance);
    }
}
