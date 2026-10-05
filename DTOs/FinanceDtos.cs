using System.ComponentModel.DataAnnotations;

namespace otw.fings.api.management.DTOs;

public sealed record HouseholdResponse(Guid Id, string Name, string Currency, string TimeZone, HouseholdRole Role);

public sealed record CreateHouseholdRequest(
    [Required, MaxLength(200)] string Name,
    [StringLength(3, MinimumLength = 3)] string Currency = "EUR",
    [MaxLength(100)] string TimeZone = "Europe/Lisbon");

public sealed record AddHouseholdMemberRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    HouseholdRole Role);

public sealed record HouseholdMemberResponse(
    Guid Id,
    long UserId,
    string Name,
    string Username,
    string Email,
    bool IsActive,
    HouseholdRole Role);

public sealed record HouseholdRoleResponse(HouseholdRole Value, string Name);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Color,
    string? Icon,
    bool IsActive,
    IReadOnlyList<SubcategoryResponse> Subcategories);

public sealed record SubcategoryResponse(Guid Id, string Name, bool IsActive);

public sealed record CreateCategoryRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(20)] string? Color,
    [MaxLength(100)] string? Icon);

public sealed record UpdateCategoryRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(20)] string? Color,
    [MaxLength(100)] string? Icon);

public sealed record CreateSubcategoryRequest([Required, MaxLength(150)] string Name);
public sealed record UpdateSubcategoryRequest([Required, MaxLength(150)] string Name);

public sealed record BudgetAllocationRequest(Guid CategoryId, decimal MonthlyAmount);

public sealed record CreateBudgetPeriodRequest(
    [Required, MaxLength(200)] string Name,
    DateOnly StartMonth,
    DateOnly EndMonth,
    decimal MonthlyAmount,
    IReadOnlyList<BudgetAllocationRequest>? Allocations);

public sealed record BudgetAllocationResponse(Guid CategoryId, string CategoryName, decimal MonthlyAmount);

public sealed record BudgetPeriodResponse(
    Guid Id,
    string Name,
    DateOnly StartMonth,
    DateOnly EndMonth,
    decimal MonthlyAmount,
    BudgetStatus Status,
    int GeneratedIncomeCount,
    IReadOnlyList<BudgetAllocationResponse> Allocations);

public sealed record IncomeResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    IncomeOrigin Origin,
    FinancialRecordStatus Status,
    Guid? BudgetPeriodId);

public sealed record CreateExpenseRequest(
    Guid? CategoryId,
    Guid? SubcategoryId,
    DateOnly Date,
    decimal Amount,
    [Required, MaxLength(500)] string Description,
    [MaxLength(250)] string? MerchantName,
    [MaxLength(32)] string? MerchantTaxNumber,
    ExpenseOrigin Origin = ExpenseOrigin.Manual,
    IReadOnlyList<CreateExpenseLineRequest>? Lines = null);

public sealed record CreateExpenseLineRequest(
    Guid CategoryId,
    Guid? SubcategoryId,
    [Required, MaxLength(500)] string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Amount);

public sealed record UpdateExpenseRequest(
    Guid? CategoryId,
    Guid? SubcategoryId,
    DateOnly Date,
    decimal Amount,
    [Required, MaxLength(500)] string Description,
    [MaxLength(250)] string? MerchantName,
    [MaxLength(32)] string? MerchantTaxNumber,
    IReadOnlyList<CreateExpenseLineRequest>? Lines = null);

public sealed record ReplaceExpenseLinesRequest(
    [Required, MinLength(1)] IReadOnlyList<CreateExpenseLineRequest> Lines);

public sealed record ExpenseLineResponse(
    Guid Id,
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Amount,
    Guid CategoryId,
    string CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    int Position);

public sealed record ExpenseResponse(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    Guid CategoryId,
    string CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    string? MerchantName,
    string? MerchantTaxNumber,
    ExpenseOrigin Origin,
    FinancialRecordStatus Status,
    IReadOnlyList<ExpenseLineResponse> Lines);

public sealed record FrequentExpenseSuggestionResponse(
    string Description,
    string MerchantName,
    string? MerchantTaxNumber,
    Guid CategoryId,
    string CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    int OccurrenceCount,
    DateOnly LastOccurrenceDate);

public sealed record CreateRecurringExpenseRequest(
    Guid CategoryId,
    Guid? SubcategoryId,
    [Required, MaxLength(500)] string Description,
    [MaxLength(250)] string? MerchantName,
    [MaxLength(32)] string? MerchantTaxNumber,
    decimal Amount,
    RecurrenceFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool MaterializeNow = false);

public sealed record UpdateRecurringExpenseRequest(
    Guid CategoryId,
    Guid? SubcategoryId,
    [Required, MaxLength(500)] string Description,
    [MaxLength(250)] string? MerchantName,
    [MaxLength(32)] string? MerchantTaxNumber,
    decimal Amount,
    RecurrenceFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record RecurringExpenseResponse(
    Guid Id,
    Guid CategoryId,
    Guid? SubcategoryId,
    string Description,
    string? MerchantName,
    string? MerchantTaxNumber,
    decimal Amount,
    RecurrenceFrequency Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly NextOccurrenceDate,
    bool IsActive);

public sealed record RecurringExpenseMaterializationResponse(
    int CreatedCount,
    DateOnly ThroughDate,
    DateOnly NextOccurrenceDate,
    bool IsActive);

public sealed record BudgetDashboardResponse(
    DateOnly Month,
    decimal Budget,
    decimal PlannedIncome,
    decimal ConfirmedExpenses,
    decimal Available,
    IReadOnlyList<CategoryBudgetStatusResponse> Categories);

public sealed record CategoryBudgetStatusResponse(
    Guid CategoryId,
    string CategoryName,
    decimal? Budget,
    decimal Spent,
    decimal? Available);

public sealed record ReceiptLineParseResponse(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Amount,
    Guid? SuggestedCategoryId,
    string? SuggestedCategoryName,
    Guid? SuggestedSubcategoryId,
    string? SuggestedSubcategoryName,
    decimal Confidence);

public sealed record ReceiptImageQualityResponse(
    int Score,
    string Level,
    bool AcceptedWithRisk,
    IReadOnlyList<string> Warnings);

public sealed record ReceiptParseResponse(
    Guid ParseId,
    string? MerchantName,
    string? MerchantTaxNumber,
    string? DocumentNumber,
    DateOnly? PurchaseDate,
    string Currency,
    decimal? Subtotal,
    decimal? Tax,
    decimal Total,
    decimal LinesTotal,
    IReadOnlyList<ReceiptLineParseResponse> Lines,
    IReadOnlyList<string> Warnings,
    ReceiptImageQualityResponse ImageQuality);

public sealed record ValidateReceiptParseRequest(
    bool IsValid,
    [MaxLength(1000)] string? Notes);

public sealed record ReceiptParseHistoryItemResponse(
    Guid ParseId,
    DateTimeOffset ParsedAtUtc,
    string Model,
    int LineCount,
    int CategorizedLineCount,
    decimal AverageConfidence,
    ReceiptParseValidationStatus ValidationStatus,
    DateTimeOffset? ValidatedAtUtc,
    string? ValidationNotes);

public sealed record ReceiptParseQualityHistoryResponse(
    int Total,
    int Pending,
    int Valid,
    int Invalid,
    decimal? ValidRate,
    IReadOnlyList<ReceiptParseHistoryItemResponse> Items);
