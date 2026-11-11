namespace otw.fings.api.management.Domain.Entities;

public sealed class ProductType : Entity
{
    public Guid HouseholdId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Household Household { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Subcategory? Subcategory { get; set; }
    public ICollection<ShoppingListItem> ShoppingListItems { get; set; } = [];
}
