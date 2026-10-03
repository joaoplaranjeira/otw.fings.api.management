using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddNotApplicableCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO `Categories`
                    (`Id`, `HouseholdId`, `Name`, `Color`, `Icon`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), households.`Id`, 'Não aplicável', '#94A3B8', 'circle-off', 1, UTC_TIMESTAMP(), NULL
                FROM `Households` AS households
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `Categories` AS existing_categories
                    WHERE existing_categories.`HouseholdId` = households.`Id`
                      AND existing_categories.`Name` = 'Não aplicável'
                );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `Subcategories`
                    (`Id`, `CategoryId`, `Name`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), categories.`Id`, 'Não aplicável', 1, UTC_TIMESTAMP(), NULL
                FROM `Categories` AS categories
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `Subcategories` AS existing_subcategories
                    WHERE existing_subcategories.`CategoryId` = categories.`Id`
                      AND existing_subcategories.`Name` = 'Não aplicável'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty: records may already be referenced by expenses.
        }
    }
}
