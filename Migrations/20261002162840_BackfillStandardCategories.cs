using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class BackfillStandardCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO `Categories`
                    (`Id`, `HouseholdId`, `Name`, `Color`, `Icon`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), households.`Id`, standard_categories.`Name`, standard_categories.`Color`,
                       standard_categories.`Icon`, 1, UTC_TIMESTAMP(), NULL
                FROM `Households` AS households
                CROSS JOIN (
                    SELECT 'Alimentação' AS `Name`, '#22C55E' AS `Color`, 'shopping-cart' AS `Icon`
                    UNION ALL SELECT 'Habitação', '#3B82F6', 'house'
                    UNION ALL SELECT 'Transportes', '#F59E0B', 'car'
                    UNION ALL SELECT 'Saúde e bem-estar', '#EF4444', 'heart-pulse'
                    UNION ALL SELECT 'Educação', '#8B5CF6', 'graduation-cap'
                    UNION ALL SELECT 'Compras pessoais', '#EC4899', 'shopping-bag'
                    UNION ALL SELECT 'Lazer e cultura', '#14B8A6', 'clapperboard'
                    UNION ALL SELECT 'Viagens', '#06B6D4', 'plane'
                    UNION ALL SELECT 'Animais de estimação', '#84CC16', 'paw-print'
                    UNION ALL SELECT 'Serviços e subscrições', '#6366F1', 'repeat-2'
                    UNION ALL SELECT 'Finanças e impostos', '#64748B', 'landmark'
                    UNION ALL SELECT 'Outros e imprevistos', '#78716C', 'circle-ellipsis'
                ) AS standard_categories
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `Categories` AS existing_categories
                    WHERE existing_categories.`HouseholdId` = households.`Id`
                      AND existing_categories.`Name` = standard_categories.`Name`
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `Subcategories`
                    (`Id`, `CategoryId`, `Name`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), categories.`Id`, standard_subcategories.`Name`, 1, UTC_TIMESTAMP(), NULL
                FROM `Categories` AS categories
                INNER JOIN (
                    SELECT 'Alimentação' AS `CategoryName`, 'Supermercado' AS `Name`
                    UNION ALL SELECT 'Alimentação', 'Mercearia e mercado'
                    UNION ALL SELECT 'Alimentação', 'Restaurantes'
                    UNION ALL SELECT 'Alimentação', 'Takeaway e entregas'
                    UNION ALL SELECT 'Alimentação', 'Cafés e pastelarias'
                    UNION ALL SELECT 'Habitação', 'Renda ou prestação'
                    UNION ALL SELECT 'Habitação', 'Condomínio'
                    UNION ALL SELECT 'Habitação', 'Eletricidade'
                    UNION ALL SELECT 'Habitação', 'Água'
                    UNION ALL SELECT 'Habitação', 'Gás'
                    UNION ALL SELECT 'Habitação', 'Telecomunicações'
                    UNION ALL SELECT 'Habitação', 'Manutenção e reparações'
                    UNION ALL SELECT 'Habitação', 'Mobiliário e equipamentos'
                    UNION ALL SELECT 'Habitação', 'Seguro da habitação'
                    UNION ALL SELECT 'Habitação', 'IMI'
                    UNION ALL SELECT 'Transportes', 'Combustível'
                    UNION ALL SELECT 'Transportes', 'Transportes públicos'
                    UNION ALL SELECT 'Transportes', 'Táxi e TVDE'
                    UNION ALL SELECT 'Transportes', 'Portagens'
                    UNION ALL SELECT 'Transportes', 'Estacionamento'
                    UNION ALL SELECT 'Transportes', 'Manutenção e reparação'
                    UNION ALL SELECT 'Transportes', 'Seguro e IUC'
                    UNION ALL SELECT 'Transportes', 'Leasing ou aluguer'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Farmácia'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Consultas'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Exames'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Dentista'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Ótica'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Seguro ou plano de saúde'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Ginásio'
                    UNION ALL SELECT 'Saúde e bem-estar', 'Terapia'
                    UNION ALL SELECT 'Educação', 'Creche e infantário'
                    UNION ALL SELECT 'Educação', 'Propinas e mensalidades'
                    UNION ALL SELECT 'Educação', 'Livros e material escolar'
                    UNION ALL SELECT 'Educação', 'Formação e cursos'
                    UNION ALL SELECT 'Educação', 'Explicações'
                    UNION ALL SELECT 'Educação', 'Refeições escolares'
                    UNION ALL SELECT 'Educação', 'Atividades extracurriculares'
                    UNION ALL SELECT 'Compras pessoais', 'Roupa e calçado'
                    UNION ALL SELECT 'Compras pessoais', 'Higiene e cosmética'
                    UNION ALL SELECT 'Compras pessoais', 'Cabeleireiro e estética'
                    UNION ALL SELECT 'Compras pessoais', 'Eletrónica'
                    UNION ALL SELECT 'Compras pessoais', 'Presentes'
                    UNION ALL SELECT 'Lazer e cultura', 'Cinema e espetáculos'
                    UNION ALL SELECT 'Lazer e cultura', 'Livros e imprensa'
                    UNION ALL SELECT 'Lazer e cultura', 'Jogos'
                    UNION ALL SELECT 'Lazer e cultura', 'Hobbies'
                    UNION ALL SELECT 'Lazer e cultura', 'Desporto'
                    UNION ALL SELECT 'Lazer e cultura', 'Eventos'
                    UNION ALL SELECT 'Viagens', 'Alojamento'
                    UNION ALL SELECT 'Viagens', 'Voos'
                    UNION ALL SELECT 'Viagens', 'Transportes'
                    UNION ALL SELECT 'Viagens', 'Atividades e passeios'
                    UNION ALL SELECT 'Viagens', 'Seguro de viagem'
                    UNION ALL SELECT 'Animais de estimação', 'Alimentação'
                    UNION ALL SELECT 'Animais de estimação', 'Veterinário'
                    UNION ALL SELECT 'Animais de estimação', 'Medicação'
                    UNION ALL SELECT 'Animais de estimação', 'Higiene'
                    UNION ALL SELECT 'Animais de estimação', 'Acessórios'
                    UNION ALL SELECT 'Animais de estimação', 'Seguro'
                    UNION ALL SELECT 'Serviços e subscrições', 'Streaming'
                    UNION ALL SELECT 'Serviços e subscrições', 'Software e aplicações'
                    UNION ALL SELECT 'Serviços e subscrições', 'Armazenamento cloud'
                    UNION ALL SELECT 'Serviços e subscrições', 'Serviços domésticos'
                    UNION ALL SELECT 'Serviços e subscrições', 'Serviços profissionais'
                    UNION ALL SELECT 'Serviços e subscrições', 'Correios e entregas'
                    UNION ALL SELECT 'Finanças e impostos', 'Comissões bancárias'
                    UNION ALL SELECT 'Finanças e impostos', 'Juros e crédito'
                    UNION ALL SELECT 'Finanças e impostos', 'Impostos e taxas'
                    UNION ALL SELECT 'Finanças e impostos', 'Seguros pessoais'
                    UNION ALL SELECT 'Finanças e impostos', 'Donativos'
                    UNION ALL SELECT 'Outros e imprevistos', 'Imprevistos'
                    UNION ALL SELECT 'Outros e imprevistos', 'Diversos'
                ) AS standard_subcategories
                    ON standard_subcategories.`CategoryName` = categories.`Name`
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `Subcategories` AS existing_subcategories
                    WHERE existing_subcategories.`CategoryId` = categories.`Id`
                      AND existing_subcategories.`Name` = standard_subcategories.`Name`
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty: generated records cannot be distinguished safely
            // from categories that users created before this migration was applied.
        }
    }
}
