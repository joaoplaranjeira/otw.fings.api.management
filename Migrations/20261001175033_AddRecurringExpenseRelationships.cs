using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringExpenseRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_CategoryId",
                table: "RecurringExpenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_HouseholdId",
                table: "RecurringExpenses",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_SubcategoryId",
                table: "RecurringExpenses",
                column: "SubcategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_RecurringExpenses_RecurringExpenseId",
                table: "Expenses",
                column: "RecurringExpenseId",
                principalTable: "RecurringExpenses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringExpenses_Categories_CategoryId",
                table: "RecurringExpenses",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringExpenses_Households_HouseholdId",
                table: "RecurringExpenses",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringExpenses_Subcategories_SubcategoryId",
                table: "RecurringExpenses",
                column: "SubcategoryId",
                principalTable: "Subcategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_RecurringExpenses_RecurringExpenseId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringExpenses_Categories_CategoryId",
                table: "RecurringExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringExpenses_Households_HouseholdId",
                table: "RecurringExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringExpenses_Subcategories_SubcategoryId",
                table: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_RecurringExpenses_CategoryId",
                table: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_RecurringExpenses_HouseholdId",
                table: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_RecurringExpenses_SubcategoryId",
                table: "RecurringExpenses");
        }
    }
}
