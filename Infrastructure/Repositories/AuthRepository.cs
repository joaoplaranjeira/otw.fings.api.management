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
        Household household,
        HouseholdMember membership,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        membership.UserId = user.Id;
        membership.HouseholdId = household.Id;
        dbContext.Households.Add(household);
        dbContext.HouseholdMembers.Add(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
