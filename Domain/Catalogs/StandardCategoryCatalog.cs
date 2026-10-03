namespace otw.fings.api.management.Domain.Catalogs;

public static class StandardCategoryCatalog
{
    public const string NotApplicableName = "Não aplicável";

    private static readonly IReadOnlyList<StandardCategoryDefinition> Definitions =
    [
        new("Alimentação", "#22C55E", "shopping-cart",
        [
            "Supermercado", "Mercearia e mercado", "Restaurantes", "Takeaway e entregas",
            "Cafés e pastelarias"
        ]),
        new("Habitação", "#3B82F6", "house",
        [
            "Renda ou prestação", "Condomínio", "Eletricidade", "Água", "Gás", "Telecomunicações",
            "Manutenção e reparações", "Mobiliário e equipamentos", "Seguro da habitação", "IMI"
        ]),
        new("Transportes", "#F59E0B", "car",
        [
            "Combustível", "Transportes públicos", "Táxi e TVDE", "Portagens", "Estacionamento",
            "Manutenção e reparação", "Seguro e IUC", "Leasing ou aluguer"
        ]),
        new("Saúde e bem-estar", "#EF4444", "heart-pulse",
        [
            "Farmácia", "Consultas", "Exames", "Dentista", "Ótica", "Seguro ou plano de saúde",
            "Ginásio", "Terapia"
        ]),
        new("Educação", "#8B5CF6", "graduation-cap",
        [
            "Creche e infantário", "Propinas e mensalidades", "Livros e material escolar",
            "Formação e cursos", "Explicações", "Refeições escolares", "Atividades extracurriculares"
        ]),
        new("Compras pessoais", "#EC4899", "shopping-bag",
        [
            "Roupa e calçado", "Higiene e cosmética", "Cabeleireiro e estética", "Eletrónica", "Presentes"
        ]),
        new("Lazer e cultura", "#14B8A6", "clapperboard",
        [
            "Cinema e espetáculos", "Livros e imprensa", "Jogos", "Hobbies", "Desporto", "Eventos"
        ]),
        new("Viagens", "#06B6D4", "plane",
        [
            "Alojamento", "Voos", "Transportes", "Atividades e passeios", "Seguro de viagem"
        ]),
        new("Animais de estimação", "#84CC16", "paw-print",
        [
            "Alimentação", "Veterinário", "Medicação", "Higiene", "Acessórios", "Seguro"
        ]),
        new("Serviços e subscrições", "#6366F1", "repeat-2",
        [
            "Streaming", "Software e aplicações", "Armazenamento cloud", "Serviços domésticos",
            "Serviços profissionais", "Correios e entregas"
        ]),
        new("Finanças e impostos", "#64748B", "landmark",
        [
            "Comissões bancárias", "Juros e crédito", "Impostos e taxas", "Seguros pessoais", "Donativos"
        ]),
        new("Outros e imprevistos", "#78716C", "circle-ellipsis",
        [
            "Imprevistos", "Diversos"
        ]),
        new(NotApplicableName, "#94A3B8", "circle-off",
        [
        ])
    ];

    public static IReadOnlyList<Category> CreateFor(Guid householdId) => Definitions
        .Select(definition =>
        {
            var category = new Category
            {
                HouseholdId = householdId,
                Name = definition.Name,
                Color = definition.Color,
                Icon = definition.Icon
            };
            category.Subcategories = definition.Subcategories.Append(NotApplicableName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new Subcategory { CategoryId = category.Id, Name = name })
                .ToArray();
            return category;
        })
        .ToArray();

    private sealed record StandardCategoryDefinition(
        string Name,
        string Color,
        string Icon,
        IReadOnlyList<string> Subcategories);
}
