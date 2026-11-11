using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddParameterizedProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "ShoppingListItems",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HouseholdProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CategoryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SubcategoryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    NormalizedName = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    Brand = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    DefaultQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DefaultUnit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ExpectedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdProducts_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdProducts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdProducts_Subcategories_SubcategoryId",
                        column: x => x.SubcategoryId,
                        principalTable: "Subcategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_ProductId",
                table: "ShoppingListItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdProducts_CategoryId",
                table: "HouseholdProducts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdProducts_HouseholdId_IsActive",
                table: "HouseholdProducts",
                columns: new[] { "HouseholdId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdProducts_HouseholdId_NormalizedName",
                table: "HouseholdProducts",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdProducts_SubcategoryId",
                table: "HouseholdProducts",
                column: "SubcategoryId");

            migrationBuilder.Sql("""
                INSERT INTO `HouseholdProducts`
                    (`Id`, `HouseholdId`, `CategoryId`, `SubcategoryId`, `Name`, `NormalizedName`,
                     `Brand`, `DefaultQuantity`, `DefaultUnit`, `ExpectedPrice`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), h.`Id`, c.`Id`, s.`Id`, p.`Name`, p.`NormalizedName`,
                       NULL, p.`DefaultQuantity`, p.`DefaultUnit`, NULL, TRUE, UTC_TIMESTAMP(), NULL
                FROM `Households` h
                CROSS JOIN (
                    SELECT 'Leite' AS `Name`, 'LEITE' AS `NormalizedName`, 'Alimentação' AS `CategoryName`, 'Supermercado' AS `SubcategoryName`, 1.000 AS `DefaultQuantity`, 'L' AS `DefaultUnit`
                    UNION ALL SELECT 'Pão', 'PAO', 'Alimentação', 'Supermercado', 1.000, 'un'
                    UNION ALL SELECT 'Ovos', 'OVOS', 'Alimentação', 'Supermercado', 12.000, 'un'
                    UNION ALL SELECT 'Arroz', 'ARROZ', 'Alimentação', 'Supermercado', 1.000, 'kg'
                    UNION ALL SELECT 'Massa', 'MASSA', 'Alimentação', 'Supermercado', 1.000, 'embalagem'
                    UNION ALL SELECT 'Água', 'AGUA', 'Alimentação', 'Supermercado', 1.000, 'garrafa'
                    UNION ALL SELECT 'Fruta', 'FRUTA', 'Alimentação', 'Supermercado', 1.000, 'kg'
                    UNION ALL SELECT 'Legumes', 'LEGUMES', 'Alimentação', 'Supermercado', 1.000, 'kg'
                    UNION ALL SELECT 'Carne', 'CARNE', 'Alimentação', 'Supermercado', 1.000, 'kg'
                    UNION ALL SELECT 'Peixe', 'PEIXE', 'Alimentação', 'Supermercado', 1.000, 'kg'
                    UNION ALL SELECT 'Azeite', 'AZEITE', 'Alimentação', 'Supermercado', 1.000, 'garrafa'
                    UNION ALL SELECT 'Café', 'CAFE', 'Alimentação', 'Supermercado', 1.000, 'embalagem'
                    UNION ALL SELECT 'Papel higiénico', 'PAPEL HIGIENICO', 'Habitação', 'Limpeza', 1.000, 'embalagem'
                    UNION ALL SELECT 'Detergente da roupa', 'DETERGENTE DA ROUPA', 'Habitação', 'Limpeza', 1.000, 'embalagem'
                    UNION ALL SELECT 'Detergente da loiça', 'DETERGENTE DA LOICA', 'Habitação', 'Limpeza', 1.000, 'embalagem'
                    UNION ALL SELECT 'Champô', 'CHAMPO', 'Compras pessoais', 'Higiene e cosmética', 1.000, 'embalagem'
                    UNION ALL SELECT 'Pasta de dentes', 'PASTA DE DENTES', 'Compras pessoais', 'Higiene e cosmética', 1.000, 'embalagem'
                ) p
                INNER JOIN `Categories` c
                    ON c.`HouseholdId` = h.`Id` AND c.`Name` = p.`CategoryName`
                INNER JOIN `Subcategories` s
                    ON s.`CategoryId` = c.`Id` AND s.`Name` = p.`SubcategoryName`
                WHERE NOT EXISTS (
                    SELECT 1 FROM `HouseholdProducts` existing
                    WHERE existing.`HouseholdId` = h.`Id`
                      AND existing.`NormalizedName` = p.`NormalizedName`
                );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_HouseholdProducts_ProductId",
                table: "ShoppingListItems",
                column: "ProductId",
                principalTable: "HouseholdProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_HouseholdProducts_ProductId",
                table: "ShoppingListItems");

            migrationBuilder.DropTable(
                name: "HouseholdProducts");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingListItems_ProductId",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "ShoppingListItems");
        }
    }
}
