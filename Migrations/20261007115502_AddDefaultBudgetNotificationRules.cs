using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultBudgetNotificationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "NotificationRules",
                columns: new[] { "Id", "ChannelsJson", "ConditionJson", "ConditionType", "CooldownSeconds", "CreatedAtUtc", "EventType", "HouseholdId", "IsEnabled", "Name", "Priority", "RecipientConfigurationJson", "RecipientPolicy", "Scope", "TemplateKey", "UpdatedAtUtc", "UserId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000002"), "[\"WebPush\",\"InApp\"]", "{\"thresholdPercentage\":80,\"direction\":\"up\"}", "BudgetThresholdCrossed", 86400, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Budget.UsageChanged", null, true, "Orçamento a 80%", 100, null, "AllHouseholdMembers", "System", "budget-threshold-reached", null, null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "[\"WebPush\",\"InApp\"]", "{\"thresholdPercentage\":100,\"direction\":\"up\"}", "BudgetThresholdCrossed", 86400, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Budget.UsageChanged", null, true, "Orçamento a 100%", 100, null, "AllHouseholdMembers", "System", "budget-threshold-reached", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NotificationRules",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "NotificationRules",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));
        }
    }
}
