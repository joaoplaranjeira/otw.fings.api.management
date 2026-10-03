using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Services;

public sealed class RecurringExpenseMaterializer(IFinanceRepository repository) : IRecurringExpenseMaterializer
{
    public async Task<int> MaterializeDueAsync(DateOnly through, CancellationToken cancellationToken)
    {
        var rules = await repository.GetDueRecurringExpensesAsync(through, cancellationToken);
        return await MaterializeAsync(rules, through, cancellationToken);
    }

    public Task<int> MaterializeAsync(
        RecurringExpense recurringExpense,
        DateOnly through,
        CancellationToken cancellationToken) =>
        MaterializeAsync([recurringExpense], through, cancellationToken);

    private async Task<int> MaterializeAsync(
        IReadOnlyList<RecurringExpense> rules,
        DateOnly through,
        CancellationToken cancellationToken)
    {
        var expenses = new List<Expense>();
        foreach (var rule in rules)
        {
            while (rule.NextOccurrenceDate <= through && (!rule.EndDate.HasValue || rule.NextOccurrenceDate <= rule.EndDate))
            {
                var expense = new Expense
                {
                    HouseholdId = rule.HouseholdId,
                    CategoryId = rule.CategoryId,
                    SubcategoryId = rule.SubcategoryId,
                    Date = rule.NextOccurrenceDate,
                    Amount = rule.Amount,
                    Description = rule.Description,
                    MerchantName = rule.MerchantName,
                    MerchantTaxNumber = rule.MerchantTaxNumber,
                    Origin = ExpenseOrigin.Recurring,
                    RecurringExpenseId = rule.Id
                };
                expense.Lines.Add(new ExpenseLine
                {
                    ExpenseId = expense.Id,
                    CategoryId = rule.CategoryId,
                    SubcategoryId = rule.SubcategoryId,
                    Description = rule.Description,
                    Quantity = 1,
                    UnitPrice = rule.Amount,
                    Amount = rule.Amount,
                    Position = 1
                });
                expenses.Add(expense);
                rule.NextOccurrenceDate = Next(rule.NextOccurrenceDate, rule.Frequency);
            }
            if (rule.EndDate.HasValue && rule.NextOccurrenceDate > rule.EndDate)
            {
                rule.IsActive = false;
            }
        }

        if (expenses.Count == 0) return 0;
        try
        {
            await repository.MaterializeRecurringExpensesAsync(expenses, cancellationToken);
            return expenses.Count;
        }
        catch (DbUpdateException)
        {
            // A unique (rule,date) constraint makes concurrent materialization idempotent.
            return 0;
        }
    }

    internal static DateOnly Next(DateOnly current, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly => current.AddDays(7),
        RecurrenceFrequency.Monthly => current.AddMonths(1),
        RecurrenceFrequency.Quarterly => current.AddMonths(3),
        RecurrenceFrequency.Annual => current.AddYears(1),
        _ => throw new ValidationException("Periodicidade recorrente inválida.")
    };
}

public sealed class RecurringExpenseWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringExpenseWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var materializer = scope.ServiceProvider.GetRequiredService<IRecurringExpenseMaterializer>();
                var lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, lisbon).DateTime);
                var created = await materializer.MaterializeDueAsync(today, stoppingToken);
                if (created > 0) logger.LogInformation("Materialized {Count} recurring expenses.", created);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Recurring expense materialization failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
