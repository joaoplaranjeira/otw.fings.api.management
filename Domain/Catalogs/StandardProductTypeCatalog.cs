namespace otw.fings.api.management.Domain.Catalogs;

public static class StandardProductTypeCatalog
{
    public static IReadOnlyList<ProductType> CreateFor(
        Guid householdId,
        IReadOnlyList<Category> categories)
    {
        return Definitions.Select(definition =>
        {
            var category = categories.Single(x => x.Name == definition.CategoryName);
            var subcategory = category.Subcategories.Single(x => x.Name == definition.SubcategoryName);
            return new ProductType
            {
                HouseholdId = householdId,
                CategoryId = category.Id,
                SubcategoryId = subcategory.Id,
                Name = definition.Name,
                NormalizedName = ProductTypeNameNormalizer.Normalize(definition.Name)
            };
        }).ToArray();
    }

    public static IReadOnlyList<StandardProductDefinition> All => Definitions;

    private static readonly IReadOnlyList<StandardProductDefinition> Definitions =
    [
        new("Leite", "Alimentação", "Supermercado"),
        new("Pão", "Alimentação", "Supermercado"),
        new("Ovos", "Alimentação", "Supermercado"),
        new("Arroz", "Alimentação", "Supermercado"),
        new("Massa", "Alimentação", "Supermercado"),
        new("Água", "Alimentação", "Supermercado"),
        new("Fruta", "Alimentação", "Supermercado"),
        new("Legumes", "Alimentação", "Supermercado"),
        new("Carne", "Alimentação", "Supermercado"),
        new("Peixe", "Alimentação", "Supermercado"),
        new("Azeite", "Alimentação", "Supermercado"),
        new("Café", "Alimentação", "Supermercado"),
        new("Papel higiénico", "Habitação", "Limpeza"),
        new("Detergente da roupa", "Habitação", "Limpeza"),
        new("Detergente da loiça", "Habitação", "Limpeza"),
        new("Champô", "Compras pessoais", "Higiene e cosmética"),
        new("Pasta de dentes", "Compras pessoais", "Higiene e cosmética")
    ];

    public sealed record StandardProductDefinition(
        string Name,
        string CategoryName,
        string SubcategoryName);
}
