using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddFiftyPercentBudgetNotificationRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "NotificationRules",
                columns: new[] { "Id", "ChannelsJson", "ConditionJson", "ConditionType", "CooldownSeconds", "CreatedAtUtc", "EventType", "HouseholdId", "IsEnabled", "Name", "Priority", "RecipientConfigurationJson", "RecipientPolicy", "Scope", "TemplateKey", "UpdatedAtUtc", "UserId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000004"), "[\"WebPush\",\"InApp\"]", "{\"thresholdPercentage\":50,\"direction\":\"up\"}", "BudgetThresholdCrossed", 86400, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Budget.UsageChanged", null, true, "Orçamento a 50%", 100, null, "AllHouseholdMembers", "System", "budget-threshold-reached", null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "NotificationRules",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));
        }
    }
}
