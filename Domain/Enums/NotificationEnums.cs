namespace otw.fings.api.management.Domain.Enums;

public enum NotificationScope
{
    System = 1,
    Household = 2,
    User = 3
}

public enum NotificationChannelType
{
    WebPush = 1,
    Email = 2,
    InApp = 3
}

public enum NotificationDeliveryStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Failed = 4,
    Cancelled = 5
}
