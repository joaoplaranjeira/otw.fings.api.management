namespace otw.fings.api.management.Infrastructure.Repositories.Interfaces;

public interface IHouseholdInvitationRepository
{
    Task<Household?> GetHouseholdAsync(Guid householdId, CancellationToken cancellationToken);
    Task<HouseholdInvitation?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken);
    Task<HouseholdInvitation?> GetAsync(Guid householdId, Guid invitationId, CancellationToken cancellationToken);
    Task<bool> HasPendingInvitationAsync(
        Guid householdId,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<HouseholdInvitation>> GetAllAsync(Guid householdId, CancellationToken cancellationToken);
    Task AddAsync(HouseholdInvitation invitation, CancellationToken cancellationToken);
    Task AcceptAsync(
        HouseholdInvitation invitation,
        HouseholdMember membership,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
