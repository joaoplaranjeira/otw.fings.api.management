using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace otw.fings.api.management.Migrations
{
    /// <inheritdoc />
    public partial class AddGenericNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationEventOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                    AggregateId = table.Column<Guid>(type: "char(36)", nullable: true),
                    AggregateType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    PayloadJson = table.Column<string>(type: "json", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEventOutbox", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: true),
                    NotificationType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Scope = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    ConditionType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ConditionJson = table.Column<string>(type: "json", nullable: false),
                    RecipientPolicy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    RecipientConfigurationJson = table.Column<string>(type: "json", nullable: true),
                    TemplateKey = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    ChannelsJson = table.Column<string>(type: "json", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RuleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventId = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "char(36)", nullable: true),
                    NotificationType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    Body = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    ActionUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TemplateKey = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Channel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Locale = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    TitleTemplate = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false),
                    BodyTemplate = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    ActionUrlTemplate = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Endpoint = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false),
                    EndpointHash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    P256dh = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Auth = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ExpirationTimeUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    DeviceName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    UserAgent = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    NotificationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Channel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    DestinationId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "NotificationRules",
                columns: new[] { "Id", "ChannelsJson", "ConditionJson", "ConditionType", "CooldownSeconds", "CreatedAtUtc", "EventType", "HouseholdId", "IsEnabled", "Name", "Priority", "RecipientConfigurationJson", "RecipientPolicy", "Scope", "TemplateKey", "UpdatedAtUtc", "UserId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), "[\"WebPush\",\"InApp\"]", "{}", "Always", null, new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Expense.Created", null, true, "Nova despesa", 100, null, "HouseholdMembersExceptActor", "System", "expense-created", null, null });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "ActionUrlTemplate", "BodyTemplate", "Channel", "CreatedAtUtc", "IsActive", "Locale", "TemplateKey", "TitleTemplate", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "/?view=expenses&expenseId={{expenseId}}", "{{actorName}} adicionou {{amount}} € — {{description}}", "WebPush", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "pt-PT", "expense-created", "Nova despesa em {{householdName}}", null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "/?view=expenses&expenseId={{expenseId}}", "{{actorName}} adicionou {{amount}} € — {{description}}", "InApp", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "pt-PT", "expense-created", "Nova despesa em {{householdName}}", null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "/?view=budget", "O agregado {{householdName}} gastou {{currentSpentAmount}} € do orçamento.", "WebPush", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "pt-PT", "budget-threshold-reached", "Orçamento em {{currentPercentage}}%", null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "/?view=budget", "O agregado {{householdName}} gastou {{currentSpentAmount}} € do orçamento.", "InApp", new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "pt-PT", "budget-threshold-reached", "Orçamento em {{currentPercentage}}%", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationId_Channel_DestinationId",
                table: "NotificationDeliveries",
                columns: new[] { "NotificationId", "Channel", "DestinationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_Status_AvailableAtUtc",
                table: "NotificationDeliveries",
                columns: new[] { "Status", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEventOutbox_HouseholdId_EventType_OccurredAtUtc",
                table: "NotificationEventOutbox",
                columns: new[] { "HouseholdId", "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEventOutbox_IdempotencyKey",
                table: "NotificationEventOutbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEventOutbox_ProcessedAtUtc_AvailableAtUtc",
                table: "NotificationEventOutbox",
                columns: new[] { "ProcessedAtUtc", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_HouseholdId_NotificationType_~",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "HouseholdId", "NotificationType", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_EventType_IsEnabled_HouseholdId",
                table: "NotificationRules",
                columns: new[] { "EventType", "IsEnabled", "HouseholdId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_DeduplicationKey",
                table: "Notifications",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAtUtc_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TemplateKey_Channel_Locale",
                table: "NotificationTemplates",
                columns: new[] { "TemplateKey", "Channel", "Locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_EndpointHash",
                table: "PushSubscriptions",
                column: "EndpointHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId_IsActive",
                table: "PushSubscriptions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "NotificationEventOutbox");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "NotificationRules");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
