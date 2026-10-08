namespace otw.fings.api.management.Services.Interfaces;

public interface IFinanceService
{
    Task<IReadOnlyList<HouseholdResponse>> GetHouseholdsAsync(long userId, CancellationToken cancellationToken);
    Task<HouseholdResponse> CreateHouseholdAsync(long userId, CreateHouseholdRequest request, CancellationToken cancellationToken);
    IReadOnlyList<HouseholdRoleResponse> GetHouseholdRoles();
    IReadOnlyList<HouseholdRelationshipResponse> GetHouseholdRelationships();
    Task<IReadOnlyList<HouseholdMemberResponse>> GetHouseholdMembersAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<HouseholdMemberResponse> AddHouseholdMemberAsync(Guid householdId, long userId, AddHouseholdMemberRequest request, CancellationToken cancellationToken);
    Task<HouseholdMemberResponse> UpdateHouseholdMemberAsync(Guid householdId, Guid memberId, long userId, UpdateHouseholdMemberRequest request, CancellationToken cancellationToken);
    Task RemoveHouseholdMemberAsync(Guid householdId, Guid memberId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryResponse>> GetCategoriesAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<CategoryResponse> CreateCategoryAsync(Guid householdId, long userId, CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryResponse> UpdateCategoryAsync(Guid householdId, Guid categoryId, long userId, UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task<SubcategoryResponse> CreateSubcategoryAsync(Guid householdId, Guid categoryId, long userId, CreateSubcategoryRequest request, CancellationToken cancellationToken);
    Task<SubcategoryResponse> UpdateSubcategoryAsync(Guid householdId, Guid categoryId, Guid subcategoryId, long userId, UpdateSubcategoryRequest request, CancellationToken cancellationToken);
    Task<BudgetPeriodResponse> CreateBudgetAsync(Guid householdId, long userId, CreateBudgetPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BudgetPeriodResponse>> GetBudgetsAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IncomeResponse>> GetIncomesAsync(Guid householdId, long userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<ExpenseResponse> CreateExpenseAsync(Guid householdId, long userId, CreateExpenseRequest request, CancellationToken cancellationToken);
    Task<ExpenseResponse> UpdateExpenseAsync(Guid householdId, Guid expenseId, long userId, UpdateExpenseRequest request, CancellationToken cancellationToken);
    Task<ExpenseResponse> ReplaceExpenseLinesAsync(Guid householdId, Guid expenseId, long userId, ReplaceExpenseLinesRequest request, CancellationToken cancellationToken);
    Task DeleteExpenseAsync(Guid householdId, Guid expenseId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExpenseResponse>> GetExpensesAsync(Guid householdId, long userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
    Task<IReadOnlyList<FrequentExpenseSuggestionResponse>> GetFrequentExpenseSuggestionsAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<BudgetDashboardResponse> GetDashboardAsync(Guid householdId, long userId, DateOnly month, CancellationToken cancellationToken);
    Task<RecurringExpenseResponse> CreateRecurringExpenseAsync(Guid householdId, long userId, CreateRecurringExpenseRequest request, CancellationToken cancellationToken);
    Task<RecurringExpenseResponse> UpdateRecurringExpenseAsync(Guid householdId, Guid recurringExpenseId, long userId, UpdateRecurringExpenseRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecurringExpenseResponse>> GetRecurringExpensesAsync(Guid householdId, long userId, CancellationToken cancellationToken);
    Task<RecurringExpenseMaterializationResponse> MaterializeRecurringExpenseAsync(Guid householdId, Guid recurringExpenseId, long userId, CancellationToken cancellationToken);
}

public interface IReceiptParser
{
    Task<ReceiptParseResponse> ParseAsync(Guid householdId, long userId, Stream image, string contentType, long length, bool acceptLowQuality, CancellationToken cancellationToken);
}

public interface IReceiptParseReviewService
{
    Task<ReceiptParseHistoryItemResponse> ValidateAsync(
        Guid householdId,
        Guid parseId,
        long userId,
        ValidateReceiptParseRequest request,
        CancellationToken cancellationToken);

    Task<ReceiptParseQualityHistoryResponse> GetHistoryAsync(
        Guid householdId,
        long userId,
        int limit,
        CancellationToken cancellationToken);
}

public interface IRecurringExpenseMaterializer
{
    Task<int> MaterializeDueAsync(DateOnly through, CancellationToken cancellationToken);
    Task<int> MaterializeAsync(RecurringExpense recurringExpense, DateOnly through, CancellationToken cancellationToken);
}
