using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListMatchQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedPurchasedQuantity",
                table: "ShoppingListExpenseMatches",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DetectedPurchasedQuantity",
                table: "ShoppingListExpenseMatches",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingQuantity",
                table: "ShoppingListExpenseMatches",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedQuantityAtDetection",
                table: "ShoppingListExpenseMatches",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedPurchasedQuantity",
                table: "ShoppingListExpenseMatches");

            migrationBuilder.DropColumn(
                name: "DetectedPurchasedQuantity",
                table: "ShoppingListExpenseMatches");

            migrationBuilder.DropColumn(
                name: "RemainingQuantity",
                table: "ShoppingListExpenseMatches");

            migrationBuilder.DropColumn(
                name: "RequestedQuantityAtDetection",
                table: "ShoppingListExpenseMatches");
        }
    }
}
