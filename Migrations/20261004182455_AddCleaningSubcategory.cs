using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddCleaningSubcategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE `Subcategories` AS subcategories
                INNER JOIN `Categories` AS categories
                    ON categories.`Id` = subcategories.`CategoryId`
                SET subcategories.`IsActive` = 1,
                    subcategories.`UpdatedAtUtc` = UTC_TIMESTAMP()
                WHERE categories.`Name` = 'Habitação'
                  AND subcategories.`Name` = 'Limpeza'
                  AND subcategories.`IsActive` = 0;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO `Subcategories`
                    (`Id`, `CategoryId`, `Name`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), categories.`Id`, 'Limpeza', 1, UTC_TIMESTAMP(), NULL
                FROM `Categories` AS categories
                WHERE categories.`Name` = 'Habitação'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM `Subcategories` AS existing_subcategories
                      WHERE existing_subcategories.`CategoryId` = categories.`Id`
                        AND existing_subcategories.`Name` = 'Limpeza'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty: the subcategory may already be referenced by expenses.
        }
    }
}
