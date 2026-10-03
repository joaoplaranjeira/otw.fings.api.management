namespace otw.fings.api.management.Domain.Entities;

public sealed class Household : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public string TimeZone { get; set; } = "Europe/Lisbon";
    public ICollection<HouseholdMember> Members { get; set; } = [];
}

public sealed class HouseholdMember : Entity
{
    public Guid HouseholdId { get; set; }
    public long UserId { get; set; }
    public HouseholdRole Role { get; set; }
    public Household Household { get; set; } = null!;
    public User User { get; set; } = null!;
}

public sealed class Category : Entity
{
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
    public Household Household { get; set; } = null!;
    public ICollection<Subcategory> Subcategories { get; set; } = [];
}

public sealed class Subcategory : Entity
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Category Category { get; set; } = null!;
}

public sealed class BudgetPeriod : Entity
{
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartMonth { get; set; }
    public DateOnly EndMonth { get; set; }
    public decimal MonthlyAmount { get; set; }
    public BudgetStatus Status { get; set; } = BudgetStatus.Active;
    public Household Household { get; set; } = null!;
    public ICollection<BudgetCategoryAllocation> Allocations { get; set; } = [];
    public ICollection<Income> GeneratedIncomes { get; set; } = [];
}

public sealed class BudgetCategoryAllocation : Entity
{
    public Guid BudgetPeriodId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal MonthlyAmount { get; set; }
    public BudgetPeriod BudgetPeriod { get; set; } = null!;
    public Category Category { get; set; } = null!;
}

public sealed class Income : Entity
{
    public Guid HouseholdId { get; set; }
    public Guid? BudgetPeriodId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public IncomeOrigin Origin { get; set; }
    public FinancialRecordStatus Status { get; set; }
    public Household Household { get; set; } = null!;
    public BudgetPeriod? BudgetPeriod { get; set; }
}

public sealed class Expense : Entity
{
    public Guid HouseholdId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public string? MerchantTaxNumber { get; set; }
    public ExpenseOrigin Origin { get; set; } = ExpenseOrigin.Manual;
    public FinancialRecordStatus Status { get; set; } = FinancialRecordStatus.Confirmed;
    public Guid? RecurringExpenseId { get; set; }
    public Household Household { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Subcategory? Subcategory { get; set; }
    public RecurringExpense? RecurringExpense { get; set; }
    public ICollection<ExpenseLine> Lines { get; set; } = [];
}

public sealed class ExpenseLine : Entity
{
    public Guid ExpenseId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int Position { get; set; }
    public Expense Expense { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Subcategory? Subcategory { get; set; }
}

public sealed class RecurringExpense : Entity
{
    public Guid HouseholdId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public string? MerchantTaxNumber { get; set; }
    public decimal Amount { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly NextOccurrenceDate { get; set; }
    public bool IsActive { get; set; } = true;
    public Household Household { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Subcategory? Subcategory { get; set; }
    public ICollection<Expense> Expenses { get; set; } = [];
}

public sealed class ReceiptParseRecord : Entity
{
    public Guid HouseholdId { get; set; }
    public long RequestedByUserId { get; set; }
    public string Model { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int CategorizedLineCount { get; set; }
    public decimal AverageConfidence { get; set; }
    public ReceiptParseValidationStatus ValidationStatus { get; set; } = ReceiptParseValidationStatus.Pending;
    public long? ValidatedByUserId { get; set; }
    public DateTimeOffset? ValidatedAtUtc { get; set; }
    public string? ValidationNotes { get; set; }
    public Household Household { get; set; } = null!;
}
