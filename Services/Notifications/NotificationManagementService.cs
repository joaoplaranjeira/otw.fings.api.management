using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Infrastructure.Data;

namespace otw.fings.api.management.Services.Notifications;

public interface INotificationManagementService
{
    Task<IReadOnlyList<NotificationRuleResponse>> GetRulesAsync(long userId, CancellationToken cancellationToken);
    Task<NotificationRuleResponse> GetRuleAsync(Guid id, long userId, CancellationToken cancellationToken);
    Task<NotificationRuleResponse> CreateRuleAsync(NotificationRuleRequest request, long userId, CancellationToken cancellationToken);
    Task<NotificationRuleResponse> UpdateRuleAsync(Guid id, NotificationRuleRequest request, long userId, CancellationToken cancellationToken);
    Task DisableRuleAsync(Guid id, long userId, CancellationToken cancellationToken);
    Task<NotificationRuleTestResponse> TestRuleAsync(Guid id, NotificationRuleTestRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationPreferenceItem>> GetPreferencesAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationPreferenceItem>> PutPreferencesAsync(PutNotificationPreferencesRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PushSubscriptionResponse>> GetSubscriptionsAsync(long userId, CancellationToken cancellationToken);
    Task<PushSubscriptionResponse> PutSubscriptionAsync(PushSubscriptionRequest request, long userId, string? userAgent, CancellationToken cancellationToken);
    Task DeleteSubscriptionAsync(Guid id, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(long userId, bool unreadOnly, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid id, long userId, CancellationToken cancellationToken);
}

public sealed class NotificationManagementService(
    FingsDbContext dbContext,
    IEnumerable<INotificationRuleEvaluator> evaluators,
    IEnumerable<INotificationRecipientResolver> resolvers,
    IEnumerable<INotificationChannel> channels) : INotificationManagementService
{
    private readonly IReadOnlyDictionary<string, INotificationRuleEvaluator> evaluatorMap = evaluators.ToDictionary(x => x.ConditionType, StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, INotificationRecipientResolver> resolverMap = resolvers.ToDictionary(x => x.PolicyName, StringComparer.Ordinal);
    private readonly IReadOnlySet<NotificationChannelType> availableChannels = channels.Select(x => x.ChannelName).ToHashSet();

    public async Task<IReadOnlyList<NotificationRuleResponse>> GetRulesAsync(long userId, CancellationToken cancellationToken)
    {
        var householdIds = dbContext.HouseholdMembers.Where(x => x.UserId == userId).Select(x => x.HouseholdId);
        return (await dbContext.NotificationRules.AsNoTracking().Where(x => x.Scope == NotificationScope.System ||
                (x.HouseholdId.HasValue && householdIds.Contains(x.HouseholdId.Value)) ||
                (x.Scope == NotificationScope.User && x.UserId == userId))
            .OrderByDescending(x => x.Priority).ThenBy(x => x.Name).ToListAsync(cancellationToken)).Select(Map).ToArray();
    }

    public async Task<NotificationRuleResponse> GetRuleAsync(Guid id, long userId, CancellationToken cancellationToken)
    {
        var rule = await GetAccessibleRuleAsync(id, userId, cancellationToken);
        return Map(rule);
    }

    public async Task<NotificationRuleResponse> CreateRuleAsync(NotificationRuleRequest request, long userId, CancellationToken cancellationToken)
    {
        await EnsureCanManageScopeAsync(request.Scope, request.HouseholdId, request.UserId, userId, cancellationToken);
        var rule = new NotificationRule();
        Apply(rule, request);
        await ValidateRuleAsync(rule, cancellationToken);
        dbContext.NotificationRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(rule);
    }

    public async Task<NotificationRuleResponse> UpdateRuleAsync(Guid id, NotificationRuleRequest request, long userId, CancellationToken cancellationToken)
    {
        var rule = await GetAccessibleRuleAsync(id, userId, cancellationToken);
        await EnsureCanManageRuleAsync(rule, userId, cancellationToken);
        await EnsureCanManageScopeAsync(request.Scope, request.HouseholdId, request.UserId, userId, cancellationToken);
        Apply(rule, request);
        await ValidateRuleAsync(rule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(rule);
    }

    public async Task DisableRuleAsync(Guid id, long userId, CancellationToken cancellationToken)
    {
        var rule = await GetAccessibleRuleAsync(id, userId, cancellationToken);
        await EnsureCanManageRuleAsync(rule, userId, cancellationToken);
        rule.IsEnabled = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationRuleTestResponse> TestRuleAsync(Guid id, NotificationRuleTestRequest request, long userId, CancellationToken cancellationToken)
    {
        var rule = await GetAccessibleRuleAsync(id, userId, cancellationToken);
        await EnsureCanManageRuleAsync(rule, userId, cancellationToken);
        using var data = JsonDocument.Parse(request.Data.GetRawText());
        var envelope = new NotificationEventEnvelope(Guid.NewGuid(), rule.EventType, 1, request.HouseholdId,
            request.ActorUserId, request.AggregateId, null, DateTimeOffset.UtcNow, data);
        var result = await evaluatorMap[rule.ConditionType].EvaluateAsync(rule, envelope, cancellationToken);
        var recipients = result.IsMatch
            ? await resolverMap[rule.RecipientPolicy].ResolveAsync(rule, envelope, cancellationToken) : [];
        return new NotificationRuleTestResponse(result.IsMatch, recipients, result.DeduplicationSuffix);
    }

    public async Task<IReadOnlyList<NotificationPreferenceItem>> GetPreferencesAsync(long userId, CancellationToken cancellationToken) =>
        await dbContext.NotificationPreferences.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.NotificationType).ThenBy(x => x.Channel)
            .Select(x => new NotificationPreferenceItem(x.HouseholdId, x.NotificationType, x.Channel, x.IsEnabled))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NotificationPreferenceItem>> PutPreferencesAsync(PutNotificationPreferencesRequest request, long userId, CancellationToken cancellationToken)
    {
        foreach (var item in request.Preferences)
        {
            if (!NotificationEventTypes.All.Contains(item.NotificationType)) throw new ValidationException($"Tipo de notificação inválido: {item.NotificationType}.");
            if (item.HouseholdId.HasValue && !await dbContext.HouseholdMembers.AnyAsync(x => x.HouseholdId == item.HouseholdId && x.UserId == userId, cancellationToken))
                throw new ForbiddenException("Não pode configurar preferências para outro agregado.");
            var preference = await dbContext.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == userId &&
                x.HouseholdId == item.HouseholdId && x.NotificationType == item.NotificationType && x.Channel == item.Channel, cancellationToken);
            if (preference is null)
            {
                dbContext.NotificationPreferences.Add(new NotificationPreference
                {
                    UserId = userId, HouseholdId = item.HouseholdId, NotificationType = item.NotificationType,
                    Channel = item.Channel, IsEnabled = item.IsEnabled
                });
            }
            else preference.IsEnabled = item.IsEnabled;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetPreferencesAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<PushSubscriptionResponse>> GetSubscriptionsAsync(long userId, CancellationToken cancellationToken) =>
        await dbContext.PushSubscriptions.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => new PushSubscriptionResponse(
                x.Id, x.Endpoint, x.ExpirationTimeUtc, x.DeviceName, x.IsActive, x.CreatedAtUtc, x.LastUsedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<PushSubscriptionResponse> PutSubscriptionAsync(PushSubscriptionRequest request, long userId, string? userAgent, CancellationToken cancellationToken)
    {
        ValidateEndpoint(request.Endpoint);
        var endpoint = request.Endpoint.Trim();
        var endpointHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint))).ToLowerInvariant();
        var subscription = await dbContext.PushSubscriptions.SingleOrDefaultAsync(x => x.EndpointHash == endpointHash, cancellationToken);
        if (subscription is not null && subscription.UserId != userId) throw new ConflictException("Esta subscrição já pertence a outro utilizador.");
        if (subscription is null)
        {
            subscription = new PushSubscription { UserId = userId, Endpoint = endpoint, EndpointHash = endpointHash };
            dbContext.PushSubscriptions.Add(subscription);
        }
        subscription.Endpoint = endpoint;
        subscription.EndpointHash = endpointHash;
        subscription.P256dh = request.P256dh.Trim();
        subscription.Auth = request.Auth.Trim();
        subscription.ExpirationTimeUtc = request.ExpirationTimeUtc;
        subscription.DeviceName = EmptyToNull(request.DeviceName);
        subscription.UserAgent = userAgent is { Length: > 500 } ? userAgent[..500] : userAgent;
        subscription.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(subscription);
    }

    public async Task DeleteSubscriptionAsync(Guid id, long userId, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.PushSubscriptions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Subscrição não encontrada.");
        subscription.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(long userId, bool unreadOnly, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking().Where(x => x.UserId == userId && (!unreadOnly || x.ReadAtUtc == null))
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).Select(x => new NotificationResponse(
                x.Id, x.HouseholdId, x.NotificationType, x.Title, x.Body, x.ActionUrl, x.CreatedAtUtc, x.ReadAtUtc))
            .ToListAsync(cancellationToken);

    public async Task MarkReadAsync(Guid id, long userId, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Notificação não encontrada.");
        notification.ReadAtUtc ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateRuleAsync(NotificationRule rule, CancellationToken cancellationToken)
    {
        if (!NotificationEventTypes.All.Contains(rule.EventType)) throw new ValidationException("O tipo de evento não existe.");
        if (!evaluatorMap.TryGetValue(rule.ConditionType, out var evaluator)) throw new ValidationException("O tipo de condição não existe.");
        if (!evaluator.SupportedEventTypes.Contains(rule.EventType)) throw new ValidationException("A condição não é compatível com o evento.");
        evaluator.Validate(rule);
        if (!resolverMap.ContainsKey(rule.RecipientPolicy)) throw new ValidationException("A política de destinatários não existe.");
        var configuredChannels = JsonSerializer.Deserialize<NotificationChannelType[]>(rule.ChannelsJson, EnumJsonOptions) ?? [];
        if (configuredChannels.Length == 0 || configuredChannels.Distinct().Count() != configuredChannels.Length) throw new ValidationException("Indique pelo menos um canal, sem duplicados.");
        if (configuredChannels.Any(x => !availableChannels.Contains(x))) throw new ValidationException("Um dos canais indicados não está disponível.");
        foreach (var channel in configuredChannels)
        {
            var template = await dbContext.NotificationTemplates.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TemplateKey == rule.TemplateKey && x.Channel == channel && x.Locale == "pt-PT" && x.IsActive, cancellationToken)
                ?? throw new ValidationException($"O template não existe para o canal {channel}.");
            NotificationTemplateRenderer.ValidateTokens(template, rule.EventType);
        }
        if (rule.CooldownSeconds is < 0) throw new ValidationException("O cooldown não pode ser negativo.");
    }

    private async Task EnsureCanManageScopeAsync(NotificationScope scope, Guid? householdId, long? scopedUserId, long userId, CancellationToken cancellationToken)
    {
        if (scope == NotificationScope.Household)
        {
            if (!householdId.HasValue || scopedUserId.HasValue) throw new ValidationException("Uma regra Household requer apenas HouseholdId.");
            var role = await dbContext.HouseholdMembers.Where(x => x.HouseholdId == householdId && x.UserId == userId).Select(x => x.Role).SingleOrDefaultAsync(cancellationToken);
            if (role is not HouseholdRole.Owner and not HouseholdRole.Administrator) throw new ForbiddenException("Apenas owners e administradores podem gerir regras do agregado.");
        }
        else if (scope == NotificationScope.User)
        {
            if (scopedUserId != userId || householdId.HasValue) throw new ForbiddenException("Só pode criar regras para o próprio utilizador.");
        }
        else
        {
            if (householdId.HasValue || scopedUserId.HasValue) throw new ValidationException("Uma regra System não aceita HouseholdId nem UserId.");
            var isAdmin = await dbContext.Users.AnyAsync(x => x.Id == userId && x.IsActive && x.Role == "Admin", cancellationToken);
            if (!isAdmin) throw new ForbiddenException("Apenas administradores globais podem gerir regras de sistema.");
        }
    }

    private Task EnsureCanManageRuleAsync(NotificationRule rule, long userId, CancellationToken cancellationToken) =>
        EnsureCanManageScopeAsync(rule.Scope, rule.HouseholdId, rule.UserId, userId, cancellationToken);

    private async Task<NotificationRule> GetAccessibleRuleAsync(Guid id, long userId, CancellationToken cancellationToken)
    {
        var rule = await dbContext.NotificationRules.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Regra de notificação não encontrada.");
        if (rule.Scope == NotificationScope.System) return rule;
        if (rule.Scope == NotificationScope.User && rule.UserId == userId) return rule;
        if (rule.HouseholdId.HasValue && await dbContext.HouseholdMembers.AnyAsync(x => x.HouseholdId == rule.HouseholdId && x.UserId == userId, cancellationToken)) return rule;
        throw new NotFoundException("Regra de notificação não encontrada.");
    }

    private static void Apply(NotificationRule rule, NotificationRuleRequest request)
    {
        rule.Name = request.Name.Trim();
        rule.EventType = request.EventType.Trim();
        rule.Scope = request.Scope;
        rule.HouseholdId = request.HouseholdId;
        rule.UserId = request.UserId;
        rule.ConditionType = request.ConditionType.Trim();
        rule.ConditionJson = request.Condition.GetRawText();
        rule.RecipientPolicy = request.RecipientPolicy.Trim();
        rule.RecipientConfigurationJson = request.RecipientConfiguration?.GetRawText();
        rule.TemplateKey = request.TemplateKey.Trim();
        rule.ChannelsJson = JsonSerializer.Serialize(request.Channels, EnumJsonOptions);
        rule.CooldownSeconds = request.CooldownSeconds;
        rule.Priority = request.Priority;
        rule.IsEnabled = request.IsEnabled;
    }

    private static NotificationRuleResponse Map(NotificationRule rule) => new(
        rule.Id, rule.Name, rule.EventType, rule.Scope, rule.HouseholdId, rule.UserId, rule.ConditionType,
        JsonDocument.Parse(rule.ConditionJson).RootElement.Clone(), rule.RecipientPolicy,
        rule.RecipientConfigurationJson is null ? null : JsonDocument.Parse(rule.RecipientConfigurationJson).RootElement.Clone(),
        rule.TemplateKey, JsonSerializer.Deserialize<NotificationChannelType[]>(rule.ChannelsJson, EnumJsonOptions) ?? [],
        rule.CooldownSeconds, rule.Priority, rule.IsEnabled, rule.CreatedAtUtc, rule.UpdatedAtUtc);

    private static PushSubscriptionResponse Map(PushSubscription item) => new(
        item.Id, item.Endpoint, item.ExpirationTimeUtc, item.DeviceName, item.IsActive, item.CreatedAtUtc, item.LastUsedAtUtc);

    private static readonly JsonSerializerOptions EnumJsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            throw new ValidationException("O endpoint Web Push tem de ser um URL HTTPS absoluto.");
        if (uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(uri.Host, out _))
            throw new ValidationException("O endpoint Web Push não pode apontar para endereços locais ou IPs diretos.");
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ValidationException("O endpoint Web Push é inválido.");
    }
}
