using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Services.Notifications;

public interface INotificationEventProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}

public sealed class NotificationEventProcessor(
    FingsDbContext dbContext,
    IEnumerable<INotificationRuleEvaluator> evaluators,
    IEnumerable<INotificationRecipientResolver> resolvers,
    INotificationTemplateRenderer renderer,
    IOptions<NotificationSettings> options,
    ILogger<NotificationEventProcessor> logger) : INotificationEventProcessor
{
    private readonly IReadOnlyDictionary<string, INotificationRuleEvaluator> evaluatorMap = evaluators.ToDictionary(x => x.ConditionType, StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, INotificationRecipientResolver> resolverMap = resolvers.ToDictionary(x => x.PolicyName, StringComparer.Ordinal);

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var events = await ClaimEventsAsync(now, cancellationToken);
        foreach (var item in events)
        {
            try
            {
                await ProcessAsync(item, cancellationToken);
                item.ProcessedAtUtc = DateTimeOffset.UtcNow;
                item.LastError = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                item.Attempts++;
                item.LastError = Truncate(exception.Message, 2000);
                item.AvailableAtUtc = DateTimeOffset.UtcNow.Add(RetryDelay(item.Attempts));
                if (item.Attempts >= 5) item.ProcessedAtUtc = DateTimeOffset.UtcNow;
                logger.LogError(exception, "Notification event {EventId} ({EventType}) failed on attempt {Attempt}.", item.Id, item.EventType, item.Attempts);
            }
            finally
            {
                item.LockedUntilUtc = null;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return events.Count;
    }

    private async Task<List<NotificationEventOutbox>> ClaimEventsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational()) transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var events = await dbContext.NotificationEventOutbox
                .Where(x => x.ProcessedAtUtc == null && x.AvailableAtUtc <= now && (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
                .OrderBy(x => x.OccurredAtUtc).Take(options.Value.EventBatchSize).ToListAsync(cancellationToken);
            foreach (var item in events) item.LockedUntilUtc = now.AddMinutes(5);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return events;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task ProcessAsync(NotificationEventOutbox item, CancellationToken cancellationToken)
    {
        if (item.SchemaVersion != 1) throw new ValidationException($"Versão {item.SchemaVersion} do evento {item.EventType} não suportada.");
        using var data = JsonDocument.Parse(item.PayloadJson);
        var envelope = new NotificationEventEnvelope(item.Id, item.EventType, item.SchemaVersion, item.HouseholdId,
            item.ActorUserId, item.AggregateId, item.AggregateType, item.OccurredAtUtc, data);
        var rules = await dbContext.NotificationRules.AsNoTracking().Where(x => x.IsEnabled && x.EventType == item.EventType &&
            (x.Scope == NotificationScope.System ||
             (x.Scope == NotificationScope.Household && x.HouseholdId == item.HouseholdId) ||
             (x.Scope == NotificationScope.User && x.UserId == item.ActorUserId)))
            .OrderByDescending(x => x.Priority).ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (!evaluatorMap.TryGetValue(rule.ConditionType, out var evaluator)) throw new ValidationException($"A condição '{rule.ConditionType}' não está disponível.");
            var result = await evaluator.EvaluateAsync(rule, envelope, cancellationToken);
            if (!result.IsMatch) continue;
            if (!resolverMap.TryGetValue(rule.RecipientPolicy, out var resolver)) throw new ValidationException($"A política '{rule.RecipientPolicy}' não está disponível.");
            var recipients = await resolver.ResolveAsync(rule, envelope, cancellationToken);
            var channels = JsonSerializer.Deserialize<NotificationChannelType[]>(rule.ChannelsJson, EnumJsonOptions) ?? [];
            foreach (var userId in recipients)
            {
                if (rule.CooldownSeconds.HasValue && await IsCoolingDownAsync(rule, userId, now: DateTimeOffset.UtcNow, cancellationToken)) continue;
                var enabledChannels = await EnabledChannelsAsync(userId, item.HouseholdId, item.EventType, channels, cancellationToken);
                if (enabledChannels.Count == 0) continue;
                var template = await ResolveTemplateAsync(rule.TemplateKey, enabledChannels[0], cancellationToken);
                var rendered = await renderer.RenderAsync(template, envelope, userId, cancellationToken);
                var deduplicationKey = result.DeduplicationSuffix is null
                    ? $"event:{item.Id}:rule:{rule.Id}" : $"rule:{rule.Id}:{result.DeduplicationSuffix}";
                if (await dbContext.Notifications.AnyAsync(x => x.UserId == userId && x.DeduplicationKey == deduplicationKey, cancellationToken)) continue;
                var notification = new Notification
                {
                    RuleId = rule.Id,
                    EventId = item.Id,
                    UserId = userId,
                    HouseholdId = item.HouseholdId,
                    NotificationType = item.EventType,
                    Title = rendered.Title,
                    Body = rendered.Body,
                    ActionUrl = rendered.ActionUrl,
                    DeduplicationKey = deduplicationKey
                };
                foreach (var channel in enabledChannels)
                {
                    if (channel == NotificationChannelType.WebPush)
                    {
                        var destinations = await dbContext.PushSubscriptions.Where(x => x.UserId == userId && x.IsActive)
                            .Select(x => x.Id).ToListAsync(cancellationToken);
                        foreach (var destination in destinations) notification.Deliveries.Add(NewDelivery(notification.Id, channel, destination));
                    }
                    else
                    {
                        notification.Deliveries.Add(NewDelivery(notification.Id, channel, null));
                    }
                }
                if (notification.Deliveries.Count > 0 || enabledChannels.Contains(NotificationChannelType.InApp)) dbContext.Notifications.Add(notification);
            }
        }
    }

    private async Task<List<NotificationChannelType>> EnabledChannelsAsync(long userId, Guid? householdId, string notificationType,
        IReadOnlyList<NotificationChannelType> channels, CancellationToken cancellationToken)
    {
        var preferences = await dbContext.NotificationPreferences.AsNoTracking().Where(x => x.UserId == userId &&
            x.NotificationType == notificationType && (x.HouseholdId == householdId || x.HouseholdId == null))
            .ToListAsync(cancellationToken);
        return channels.Distinct().Where(channel =>
        {
            var preference = preferences.FirstOrDefault(x => x.HouseholdId == householdId && x.Channel == channel)
                ?? preferences.FirstOrDefault(x => x.HouseholdId == null && x.Channel == channel);
            return preference?.IsEnabled ?? (options.Value.DefaultEnabledByType.TryGetValue(notificationType, out var configured)
                ? configured : options.Value.DefaultEnabled);
        }).ToList();
    }

    private Task<bool> IsCoolingDownAsync(NotificationRule rule, long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Notifications.AnyAsync(x => x.RuleId == rule.Id && x.UserId == userId &&
            x.CreatedAtUtc >= now.AddSeconds(-rule.CooldownSeconds!.Value), cancellationToken);

    private async Task<NotificationTemplate> ResolveTemplateAsync(string key, NotificationChannelType channel, CancellationToken cancellationToken) =>
        await dbContext.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TemplateKey == key && x.Channel == channel && x.Locale == "pt-PT" && x.IsActive, cancellationToken)
        ?? throw new ValidationException($"Template '{key}' não encontrado para {channel}.");

    private static NotificationDelivery NewDelivery(Guid notificationId, NotificationChannelType channel, Guid? destinationId) => new()
    {
        NotificationId = notificationId, Channel = channel, DestinationId = destinationId, AvailableAtUtc = DateTimeOffset.UtcNow
    };

    private static TimeSpan RetryDelay(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1), 2 => TimeSpan.FromMinutes(5), 3 => TimeSpan.FromMinutes(30), _ => TimeSpan.FromHours(2)
    };
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static readonly JsonSerializerOptions EnumJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };
}

public interface INotificationDeliveryProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}

public sealed class NotificationDeliveryProcessor(
    FingsDbContext dbContext,
    IEnumerable<INotificationChannel> channels,
    IOptions<NotificationSettings> options,
    ILogger<NotificationDeliveryProcessor> logger) : INotificationDeliveryProcessor
{
    private readonly IReadOnlyDictionary<NotificationChannelType, INotificationChannel> channelMap = channels.ToDictionary(x => x.ChannelName);
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var deliveries = await ClaimDeliveriesAsync(now, cancellationToken);
        foreach (var delivery in deliveries)
        {
            try
            {
                if (!channelMap.TryGetValue(delivery.Channel, out var channel))
                    throw new ValidationException($"Canal {delivery.Channel} indisponível.");
                delivery.Attempts++;
                var result = await channel.SendAsync(delivery.Notification, delivery, cancellationToken);
                delivery.LastError = result.Error;
                if (result.Success)
                {
                    delivery.Status = NotificationDeliveryStatus.Sent;
                    delivery.SentAtUtc = DateTimeOffset.UtcNow;
                    delivery.ProviderMessageId = result.ProviderMessageId;
                }
                else if (result.DestinationInvalid)
                {
                    delivery.Status = NotificationDeliveryStatus.Cancelled;
                }
                else if (result.IsTransient && delivery.Attempts < 5)
                {
                    delivery.Status = NotificationDeliveryStatus.Pending;
                    delivery.AvailableAtUtc = DateTimeOffset.UtcNow.Add(RetryDelay(delivery.Attempts));
                }
                else delivery.Status = NotificationDeliveryStatus.Failed;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                delivery.Attempts++;
                delivery.LastError = exception.Message.Length <= 2000 ? exception.Message : exception.Message[..2000];
                delivery.Status = delivery.Attempts < 5 ? NotificationDeliveryStatus.Pending : NotificationDeliveryStatus.Failed;
                delivery.AvailableAtUtc = DateTimeOffset.UtcNow.Add(RetryDelay(delivery.Attempts));
                logger.LogError(exception, "Notification delivery {DeliveryId} failed on attempt {Attempt}.", delivery.Id, delivery.Attempts);
            }
            delivery.LockedUntilUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return deliveries.Count;
    }

    private async Task<List<NotificationDelivery>> ClaimDeliveriesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (dbContext.Database.IsRelational()) transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var deliveries = await dbContext.NotificationDeliveries.Include(x => x.Notification)
                .Where(x => (x.Status == NotificationDeliveryStatus.Pending || x.Status == NotificationDeliveryStatus.Processing) &&
                    x.AvailableAtUtc <= now && (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
                .OrderBy(x => x.AvailableAtUtc).Take(options.Value.DeliveryBatchSize).ToListAsync(cancellationToken);
            foreach (var delivery in deliveries)
            {
                delivery.Status = NotificationDeliveryStatus.Processing;
                delivery.LockedUntilUtc = now.AddMinutes(5);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return deliveries;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static TimeSpan RetryDelay(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.Zero, 2 => TimeSpan.FromMinutes(1), 3 => TimeSpan.FromMinutes(5), 4 => TimeSpan.FromMinutes(30), _ => TimeSpan.FromHours(2)
    };
}

public sealed class NotificationEventWorker(IServiceScopeFactory scopeFactory, IOptions<NotificationSettings> options, ILogger<NotificationEventWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds)));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<INotificationEventProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Notification event worker iteration failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class NotificationDeliveryWorker(IServiceScopeFactory scopeFactory, IOptions<NotificationSettings> options, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds)));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<INotificationDeliveryProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Notification delivery worker iteration failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
