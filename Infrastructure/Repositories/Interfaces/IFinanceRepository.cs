namespace otw.fings.api.management.Infrastructure.Repositories.Interfaces;

public interface IFinanceRepository
{
    Task<bool> IsMemberAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<HouseholdRole?> GetMemberRoleAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HouseholdResponse>> GetHouseholdsAsync(long userId, CancellationToken cancellationToken);
    Task AddHouseholdAsync(
        Household household,
        HouseholdMember membership,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(Guid householdId, CancellationToken cancellationToken);
    Task<Category?> GetCategoryAsync(Guid householdId, Guid categoryId, CancellationToken cancellationToken);
    Task<Subcategory?> GetSubcategoryAsync(Guid categoryId, Guid subcategoryId, CancellationToken cancellationToken);
    Task AddCategoryAsync(Category category, CancellationToken cancellationToken);
    Task AddSubcategoryAsync(Subcategory subcategory, CancellationToken cancellationToken);
    Task<bool> HasOverlappingBudgetAsync(Guid householdId, DateOnly startMonth, DateOnly endMonth, CancellationToken cancellationToken);
    Task AddBudgetAsync(BudgetPeriod budget, IReadOnlyList<Income> incomes, CancellationToken cancellationToken);
    Task<IReadOnlyList<BudgetPeriod>> GetBudgetsAsync(Guid householdId, CancellationToken cancellationToken);
    Task<BudgetPeriod?> GetBudgetForMonthAsync(Guid householdId, DateOnly month, CancellationToken cancellationToken);
    Task<IReadOnlyList<Income>> GetIncomesAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task AddExpenseAsync(Expense expense, CancellationToken cancellationToken);
    Task<Expense?> GetExpenseAsync(Guid householdId, Guid expenseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Expense>> GetExpensesAsync(Guid householdId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task AddRecurringExpenseAsync(RecurringExpense recurringExpense, CancellationToken cancellationToken);
    Task<RecurringExpense?> GetRecurringExpenseAsync(Guid householdId, Guid recurringExpenseId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RecurringExpense>> GetRecurringExpensesAsync(Guid householdId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecurringExpense>> GetDueRecurringExpensesAsync(DateOnly through, CancellationToken cancellationToken);
    Task MaterializeRecurringExpensesAsync(IReadOnlyList<Expense> expenses, CancellationToken cancellationToken);
    Task AddReceiptParseRecordAsync(ReceiptParseRecord record, CancellationToken cancellationToken);
    Task<ReceiptParseRecord?> GetReceiptParseRecordAsync(Guid householdId, Guid parseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReceiptParseRecord>> GetReceiptParseRecordsAsync(Guid householdId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<ReceiptParseValidationStatus, int>> GetReceiptParseStatusCountsAsync(
        Guid householdId,
        CancellationToken cancellationToken);
}
