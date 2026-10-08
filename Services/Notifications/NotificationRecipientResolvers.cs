using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using otw.fings.api.management.Infrastructure.Data;

namespace otw.fings.api.management.Services.Notifications;

public abstract class HouseholdRecipientResolver(FingsDbContext dbContext) : INotificationRecipientResolver
{
    public abstract string PolicyName { get; }
    protected FingsDbContext DbContext { get; } = dbContext;
    protected abstract IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent);

    public virtual async Task<IReadOnlyList<long>> ResolveAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken)
    {
        if (!notificationEvent.HouseholdId.HasValue) return [];
        var query = DbContext.HouseholdMembers.AsNoTracking().Where(x =>
            x.HouseholdId == notificationEvent.HouseholdId && x.UserId.HasValue && x.User!.IsActive);
        return await ApplyPolicy(query, notificationEvent).Select(x => x.UserId!.Value).Distinct().ToListAsync(cancellationToken);
    }
}

public sealed class AllHouseholdMembersResolver(FingsDbContext db) : HouseholdRecipientResolver(db)
{
    public override string PolicyName => "AllHouseholdMembers";
    protected override IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent) => query;
}

public sealed class HouseholdMembersExceptActorResolver(FingsDbContext db) : HouseholdRecipientResolver(db)
{
    public override string PolicyName => "HouseholdMembersExceptActor";
    protected override IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent) =>
        notificationEvent.ActorUserId.HasValue ? query.Where(x => x.UserId != notificationEvent.ActorUserId) : query;
}

public sealed class HouseholdOwnersResolver(FingsDbContext db) : HouseholdRecipientResolver(db)
{
    public override string PolicyName => "HouseholdOwners";
    protected override IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent) => query.Where(x => x.Role == HouseholdRole.Owner);
}

public sealed class HouseholdManagersResolver(FingsDbContext db) : HouseholdRecipientResolver(db)
{
    public override string PolicyName => "HouseholdManagers";
    protected override IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent) =>
        query.Where(x => x.Role == HouseholdRole.Owner || x.Role == HouseholdRole.Administrator);
}

public sealed class ActorOnlyResolver(FingsDbContext db) : HouseholdRecipientResolver(db)
{
    public override string PolicyName => "ActorOnly";
    protected override IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent) =>
        query.Where(x => x.UserId == notificationEvent.ActorUserId);
}

public sealed class SpecificUsersResolver(FingsDbContext db) : HouseholdRecipientResolver(db)
{
    public override string PolicyName => "SpecificUsers";
    protected override IQueryable<HouseholdMember> ApplyPolicy(IQueryable<HouseholdMember> query, NotificationEventEnvelope notificationEvent) => query;
    public override async Task<IReadOnlyList<long>> ResolveAsync(NotificationRule rule, NotificationEventEnvelope notificationEvent, CancellationToken cancellationToken)
    {
        var configured = JsonSerializer.Deserialize<SpecificUsersConfiguration>(rule.RecipientConfigurationJson ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (configured?.UserIds is not { Count: > 0 }) return [];
        var members = await base.ResolveAsync(rule, notificationEvent, cancellationToken);
        return members.Where(configured.UserIds.Contains).ToArray();
    }
    private sealed record SpecificUsersConfiguration(IReadOnlyList<long> UserIds);
}
