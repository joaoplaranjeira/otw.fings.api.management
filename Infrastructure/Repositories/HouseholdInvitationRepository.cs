using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;

namespace otw.fings.api.management.Infrastructure.Repositories;

public sealed class HouseholdInvitationRepository(FingsDbContext dbContext) : IHouseholdInvitationRepository
{
    public Task<Household?> GetHouseholdAsync(Guid householdId, CancellationToken cancellationToken) =>
        dbContext.Households.SingleOrDefaultAsync(x => x.Id == householdId, cancellationToken);

    public Task<HouseholdInvitation?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken) =>
        dbContext.HouseholdInvitations
            .Include(x => x.Household)
            .SingleOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);

    public Task<HouseholdInvitation?> GetAsync(
        Guid householdId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        dbContext.HouseholdInvitations
            .Include(x => x.Household)
            .SingleOrDefaultAsync(
                x => x.HouseholdId == householdId && x.Id == invitationId,
                cancellationToken);

    public Task<bool> HasPendingInvitationAsync(
        Guid householdId,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.HouseholdInvitations.AnyAsync(
            x => x.HouseholdId == householdId &&
                 x.Email == email &&
                 x.Status == HouseholdInvitationStatus.Pending &&
                 x.ExpiresAtUtc > now,
            cancellationToken);

    public async Task<IReadOnlyList<HouseholdInvitation>> GetAllAsync(
        Guid householdId,
        CancellationToken cancellationToken) =>
        await dbContext.HouseholdInvitations.AsNoTracking()
            .Include(x => x.Household)
            .Where(x => x.HouseholdId == householdId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(HouseholdInvitation invitation, CancellationToken cancellationToken)
    {
        dbContext.HouseholdInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AcceptAsync(
        HouseholdInvitation invitation,
        HouseholdMember membership,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        dbContext.HouseholdMembers.Add(membership);
        invitation.Status = HouseholdInvitationStatus.Accepted;
        invitation.AcceptedByUserId = membership.UserId;
        invitation.AcceptedAtUtc = acceptedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
