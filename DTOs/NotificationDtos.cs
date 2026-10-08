using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace otw.fings.api.management.DTOs;

public sealed record NotificationRuleRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(100)] string EventType,
    NotificationScope Scope,
    Guid? HouseholdId,
    long? UserId,
    [Required, MaxLength(100)] string ConditionType,
    JsonElement Condition,
    [Required, MaxLength(100)] string RecipientPolicy,
    JsonElement? RecipientConfiguration,
    [Required, MaxLength(150)] string TemplateKey,
    [Required, MinLength(1)] IReadOnlyList<NotificationChannelType> Channels,
    int? CooldownSeconds,
    int Priority = 100,
    bool IsEnabled = true);

public sealed record NotificationRuleResponse(
    Guid Id, string Name, string EventType, NotificationScope Scope, Guid? HouseholdId, long? UserId,
    string ConditionType, JsonElement Condition, string RecipientPolicy, JsonElement? RecipientConfiguration,
    string TemplateKey, IReadOnlyList<NotificationChannelType> Channels, int? CooldownSeconds,
    int Priority, bool IsEnabled, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public sealed record NotificationPreferenceItem(
    Guid? HouseholdId, [Required, MaxLength(100)] string NotificationType,
    NotificationChannelType Channel, bool IsEnabled);

public sealed record PutNotificationPreferencesRequest([Required] IReadOnlyList<NotificationPreferenceItem> Preferences);

public sealed record PushSubscriptionRequest(
    [Required, MaxLength(2048)] string Endpoint,
    [Required, MaxLength(255)] string P256dh,
    [Required, MaxLength(255)] string Auth,
    DateTimeOffset? ExpirationTimeUtc,
    [MaxLength(200)] string? DeviceName);

public sealed record PushSubscriptionResponse(
    Guid Id, string Endpoint, DateTimeOffset? ExpirationTimeUtc, string? DeviceName,
    bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastUsedAtUtc);

public sealed record NotificationResponse(
    Guid Id, Guid? HouseholdId, string NotificationType, string Title, string Body,
    string? ActionUrl, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);

public sealed record NotificationRuleTestRequest(
    Guid? HouseholdId, long? ActorUserId, Guid? AggregateId, JsonElement Data);

public sealed record NotificationRuleTestResponse(bool IsMatch, IReadOnlyList<long> RecipientUserIds, string? DeduplicationSuffix);
