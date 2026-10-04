using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    CodeHash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    EmailSent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_AcceptedByUserId",
                table: "HouseholdInvitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_CodeHash",
                table: "HouseholdInvitations",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_CreatedByUserId",
                table: "HouseholdInvitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId_Email_Status",
                table: "HouseholdInvitations",
                columns: new[] { "HouseholdId", "Email", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdInvitations");
        }
    }
}
