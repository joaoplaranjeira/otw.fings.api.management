namespace otw.fings.api.management.Domain.Entities;

public sealed class NotificationEventOutbox : Entity
{
    public string EventType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public Guid? HouseholdId { get; set; }
    public long? ActorUserId { get; set; }
    public Guid? AggregateId { get; set; }
    public string? AggregateType { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
}

public sealed class NotificationRule : Entity
{
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public NotificationScope Scope { get; set; }
    public Guid? HouseholdId { get; set; }
    public long? UserId { get; set; }
    public string ConditionType { get; set; } = string.Empty;
    public string ConditionJson { get; set; } = "{}";
    public string RecipientPolicy { get; set; } = string.Empty;
    public string? RecipientConfigurationJson { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string ChannelsJson { get; set; } = "[]";
    public int? CooldownSeconds { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsEnabled { get; set; } = true;
}

public sealed class NotificationPreference : Entity
{
    public long UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public NotificationChannelType Channel { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class NotificationTemplate : Entity
{
    public string TemplateKey { get; set; } = string.Empty;
    public NotificationChannelType Channel { get; set; }
    public string Locale { get; set; } = "pt-PT";
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Notification : Entity
{
    public Guid RuleId { get; set; }
    public Guid EventId { get; set; }
    public long UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public DateTimeOffset? ReadAtUtc { get; set; }
    public ICollection<NotificationDelivery> Deliveries { get; set; } = [];
}

public sealed class NotificationDelivery : Entity
{
    public Guid NotificationId { get; set; }
    public NotificationChannelType Channel { get; set; }
    public Guid? DestinationId { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public Notification Notification { get; set; } = null!;
}

public sealed class PushSubscription : Entity
{
    public long UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string EndpointHash { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public DateTimeOffset? ExpirationTimeUtc { get; set; }
    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastUsedAtUtc { get; set; }
}
