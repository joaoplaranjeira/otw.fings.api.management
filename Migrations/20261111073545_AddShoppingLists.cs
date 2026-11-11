using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShoppingLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingLists_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingLists_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ShoppingListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShoppingListId = table.Column<Guid>(type: "char(36)", nullable: false),
                    AddedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    ProductName = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    NormalizedProductName = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    Brand = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Variant = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    DetectionConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_ShoppingLists_ShoppingListId",
                        column: x => x.ShoppingListId,
                        principalTable: "ShoppingLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShoppingListItems_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ShoppingListExpenseMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ShoppingListItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ExpenseLineId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ExpenseLineDescription = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShoppingListExpenseMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShoppingListExpenseMatches_ExpenseLines_ExpenseLineId",
                        column: x => x.ExpenseLineId,
                        principalTable: "ExpenseLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShoppingListExpenseMatches_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShoppingListExpenseMatches_ShoppingListItems_ShoppingListIte~",
                        column: x => x.ShoppingListItemId,
                        principalTable: "ShoppingListItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShoppingListExpenseMatches_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListExpenseMatches_ExpenseId",
                table: "ShoppingListExpenseMatches",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListExpenseMatches_ExpenseLineId",
                table: "ShoppingListExpenseMatches",
                column: "ExpenseLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListExpenseMatches_ReviewedByUserId",
                table: "ShoppingListExpenseMatches",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListExpenseMatches_ShoppingListItemId_ExpenseLineId",
                table: "ShoppingListExpenseMatches",
                columns: new[] { "ShoppingListItemId", "ExpenseLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListExpenseMatches_Status_CreatedAtUtc",
                table: "ShoppingListExpenseMatches",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_AddedByUserId",
                table: "ShoppingListItems",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_CompletedByUserId",
                table: "ShoppingListItems",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_NormalizedProductName",
                table: "ShoppingListItems",
                column: "NormalizedProductName");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingListItems_ShoppingListId_Status",
                table: "ShoppingListItems",
                columns: new[] { "ShoppingListId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_CreatedByUserId",
                table: "ShoppingLists",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingLists_HouseholdId_Status",
                table: "ShoppingLists",
                columns: new[] { "HouseholdId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShoppingListExpenseMatches");

            migrationBuilder.DropTable(
                name: "ShoppingListItems");

            migrationBuilder.DropTable(
                name: "ShoppingLists");
        }
    }
}
