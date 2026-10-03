using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptParseQualityHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptParseRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Model = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    LineCount = table.Column<int>(type: "int", nullable: false),
                    CategorizedLineCount = table.Column<int>(type: "int", nullable: false),
                    AverageConfidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    ValidationStatus = table.Column<int>(type: "int", nullable: false),
                    ValidatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ValidatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    ValidationNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptParseRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptParseRecords_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptParseRecords_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptParseRecords_Users_ValidatedByUserId",
                        column: x => x.ValidatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptParseRecords_HouseholdId_CreatedAtUtc",
                table: "ReceiptParseRecords",
                columns: new[] { "HouseholdId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptParseRecords_HouseholdId_ValidationStatus",
                table: "ReceiptParseRecords",
                columns: new[] { "HouseholdId", "ValidationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptParseRecords_RequestedByUserId",
                table: "ReceiptParseRecords",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptParseRecords_ValidatedByUserId",
                table: "ReceiptParseRecords",
                column: "ValidatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptParseRecords");
        }
    }
}
