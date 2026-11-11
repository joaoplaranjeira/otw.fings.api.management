namespace otw.fings.api.management.Domain.Entities;

public sealed class ShoppingList : Entity
{
    public Guid HouseholdId { get; set; }
    public long CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ShoppingListStatus Status { get; set; } = ShoppingListStatus.Active;
    public Household Household { get; set; } = null!;
    public ICollection<ShoppingListItem> Items { get; set; } = [];
}

public sealed class ShoppingListItem : Entity
{
    public Guid ShoppingListId { get; set; }
    public long AddedByUserId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string NormalizedProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Variant { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public string? Notes { get; set; }
    public ShoppingListItemSource Source { get; set; } = ShoppingListItemSource.Manual;
    public decimal? DetectionConfidence { get; set; }
    public ShoppingListItemStatus Status { get; set; } = ShoppingListItemStatus.Active;
    public long? CompletedByUserId { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;
    public ICollection<ShoppingListExpenseMatch> ExpenseMatches { get; set; } = [];
}

public sealed class ShoppingListExpenseMatch : Entity
{
    public Guid ShoppingListItemId { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid ExpenseLineId { get; set; }
    public string ExpenseLineDescription { get; set; } = string.Empty;
    public decimal? RequestedQuantityAtDetection { get; set; }
    public decimal? DetectedPurchasedQuantity { get; set; }
    public decimal? ApprovedPurchasedQuantity { get; set; }
    public decimal? RemainingQuantity { get; set; }
    public decimal Confidence { get; set; }
    public ShoppingListMatchStatus Status { get; set; } = ShoppingListMatchStatus.Pending;
    public long? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public ShoppingListItem ShoppingListItem { get; set; } = null!;
    public Expense Expense { get; set; } = null!;
    public ExpenseLine ExpenseLine { get; set; } = null!;
}
