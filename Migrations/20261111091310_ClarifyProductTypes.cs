using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class ClarifyProductTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_HouseholdProducts_ProductId",
                table: "ShoppingListItems");

            migrationBuilder.RenameTable(
                name: "HouseholdProducts",
                newName: "ProductTypes");

            migrationBuilder.RenameColumn(
                name: "ProductName",
                table: "ShoppingListItems",
                newName: "ProductTypeName");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "ShoppingListItems",
                newName: "ProductTypeId");

            migrationBuilder.RenameColumn(
                name: "NormalizedProductName",
                table: "ShoppingListItems",
                newName: "NormalizedProductTypeName");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_ProductId",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_ProductTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_NormalizedProductName",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_NormalizedProductTypeName");

            migrationBuilder.RenameIndex(
                name: "IX_HouseholdProducts_CategoryId",
                table: "ProductTypes",
                newName: "IX_ProductTypes_CategoryId");

            migrationBuilder.RenameIndex(
                name: "IX_HouseholdProducts_HouseholdId_IsActive",
                table: "ProductTypes",
                newName: "IX_ProductTypes_HouseholdId_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_HouseholdProducts_HouseholdId_NormalizedName",
                table: "ProductTypes",
                newName: "IX_ProductTypes_HouseholdId_NormalizedName");

            migrationBuilder.RenameIndex(
                name: "IX_HouseholdProducts_SubcategoryId",
                table: "ProductTypes",
                newName: "IX_ProductTypes_SubcategoryId");

            migrationBuilder.DropColumn(name: "Brand", table: "ProductTypes");
            migrationBuilder.DropColumn(name: "DefaultQuantity", table: "ProductTypes");
            migrationBuilder.DropColumn(name: "DefaultUnit", table: "ProductTypes");
            migrationBuilder.DropColumn(name: "ExpectedPrice", table: "ProductTypes");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_ProductTypes_ProductTypeId",
                table: "ShoppingListItems",
                column: "ProductTypeId",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingListItems_ProductTypes_ProductTypeId",
                table: "ShoppingListItems");

            migrationBuilder.AddColumn<string>(
                name: "Brand", table: "ProductTypes", type: "varchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultQuantity", table: "ProductTypes", type: "decimal(18,3)", precision: 18, scale: 3, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DefaultUnit", table: "ProductTypes", type: "varchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedPrice", table: "ProductTypes", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);

            migrationBuilder.RenameColumn(
                name: "ProductTypeName",
                table: "ShoppingListItems",
                newName: "ProductName");

            migrationBuilder.RenameColumn(
                name: "ProductTypeId",
                table: "ShoppingListItems",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "NormalizedProductTypeName",
                table: "ShoppingListItems",
                newName: "NormalizedProductName");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_ProductTypeId",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_ProductId");

            migrationBuilder.RenameIndex(
                name: "IX_ShoppingListItems_NormalizedProductTypeName",
                table: "ShoppingListItems",
                newName: "IX_ShoppingListItems_NormalizedProductName");

            migrationBuilder.RenameIndex(
                name: "IX_ProductTypes_CategoryId", table: "ProductTypes", newName: "IX_HouseholdProducts_CategoryId");
            migrationBuilder.RenameIndex(
                name: "IX_ProductTypes_HouseholdId_IsActive", table: "ProductTypes", newName: "IX_HouseholdProducts_HouseholdId_IsActive");
            migrationBuilder.RenameIndex(
                name: "IX_ProductTypes_HouseholdId_NormalizedName", table: "ProductTypes", newName: "IX_HouseholdProducts_HouseholdId_NormalizedName");
            migrationBuilder.RenameIndex(
                name: "IX_ProductTypes_SubcategoryId", table: "ProductTypes", newName: "IX_HouseholdProducts_SubcategoryId");

            migrationBuilder.RenameTable(name: "ProductTypes", newName: "HouseholdProducts");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingListItems_HouseholdProducts_ProductId",
                table: "ShoppingListItems",
                column: "ProductId",
                principalTable: "HouseholdProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
