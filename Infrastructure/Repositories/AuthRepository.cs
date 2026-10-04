using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;

namespace otw.fings.api.management.Infrastructure.Repositories;

public sealed class AuthRepository(FingsDbContext dbContext) : IAuthRepository
{
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

    public Task<User?> GetUserByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<HouseholdInvitation?> GetHouseholdInvitationByCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken) =>
        dbContext.HouseholdInvitations
            .Include(x => x.Household)
            .SingleOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(long userId, CancellationToken cancellationToken) =>
        await dbContext.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PermissionKey)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public Task<OtpCode?> GetLatestOtpAsync(string email, CancellationToken cancellationToken) =>
        dbContext.OtpCodes.Where(x => x.Email == email)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountRecentOtpsAsync(string email, DateTime sinceUtc, CancellationToken cancellationToken) =>
        dbContext.OtpCodes.CountAsync(x => x.Email == email && x.CreatedAt >= sinceUtc, cancellationToken);

    public async Task AddOtpAsync(OtpCode otp, CancellationToken cancellationToken)
    {
        dbContext.OtpCodes.Add(otp);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> UpdateUserProfileAsync(
        long userId,
        string? name,
        string? username,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;

        if (name is not null) user.Name = name;
        if (username is not null) user.Username = username;
        user.UpdatedUser = user.Email;
        user.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task RegisterAsync(
        User user,
        Household? household,
        HouseholdMember? membership,
        IReadOnlyList<Category>? categories,
        HouseholdInvitation? invitation,
        DateTimeOffset? invitationAcceptedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (household is not null)
        {
            dbContext.Households.Add(household);
            if (categories is not null) dbContext.Categories.AddRange(categories);
        }

        if (membership is not null)
        {
            membership.UserId = user.Id;
            dbContext.HouseholdMembers.Add(membership);
        }

        if (invitation is not null)
        {
            invitation.Status = HouseholdInvitationStatus.Accepted;
            invitation.AcceptedByUserId = user.Id;
            invitation.AcceptedAtUtc = invitationAcceptedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
