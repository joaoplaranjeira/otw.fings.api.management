using System.Text.Json;

namespace otw.fings.api.management.Services.Notifications;

public sealed record NotificationEventEnvelope(
    Guid Id, string EventType, int SchemaVersion, Guid? HouseholdId, long? ActorUserId,
    Guid? AggregateId, string? AggregateType, DateTimeOffset OccurredAtUtc, JsonDocument Data);

public sealed record RuleEvaluationResult(bool IsMatch, string? DeduplicationSuffix = null)
{
    public static RuleEvaluationResult NoMatch { get; } = new(false);
    public static RuleEvaluationResult Match(string? suffix = null) => new(true, suffix);
}

public interface INotificationRuleEvaluator
{
    string ConditionType { get; }
    IReadOnlySet<string> SupportedEventTypes { get; }
    Task<RuleEvaluationResult> EvaluateAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken);
    void Validate(NotificationRule rule);
}

public interface INotificationRecipientResolver
{
    string PolicyName { get; }
    Task<IReadOnlyList<long>> ResolveAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken);
}

public sealed record DeliveryResult(bool Success, bool IsTransient = false, bool DestinationInvalid = false, string? ProviderMessageId = null, string? Error = null);

public interface INotificationChannel
{
    NotificationChannelType ChannelName { get; }
    Task<DeliveryResult> SendAsync(Notification notification, NotificationDelivery delivery, CancellationToken cancellationToken);
}

public static class NotificationEventTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "Expense.Created", "Expense.Updated", "Expense.Deleted", "Budget.UsageChanged",
        "Budget.ThresholdReached", "RecurringExpense.Materialized", "Household.MemberAdded",
        "Household.InvitationAccepted"
    };
}
