using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class TrackExpenseCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CreatedByUserId",
                table: "RecurringExpenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreatedByUserId",
                table: "Expenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_CreatedByUserId",
                table: "RecurringExpenses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_CreatedByUserId",
                table: "Expenses",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringExpenses_Users_CreatedByUserId",
                table: "RecurringExpenses",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_CreatedByUserId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringExpenses_Users_CreatedByUserId",
                table: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_RecurringExpenses_CreatedByUserId",
                table: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_CreatedByUserId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "RecurringExpenses");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Expenses");
        }
    }
}
