namespace otw.fings.api.management.Infrastructure.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<User?> GetUserByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetPermissionsAsync(long userId, CancellationToken cancellationToken);
    Task<OtpCode?> GetLatestOtpAsync(string email, CancellationToken cancellationToken);
    Task<int> CountRecentOtpsAsync(string email, DateTime sinceUtc, CancellationToken cancellationToken);
    Task AddOtpAsync(OtpCode otp, CancellationToken cancellationToken);
    Task<User?> UpdateUserProfileAsync(long userId, string? name, string? username, CancellationToken cancellationToken);
    Task RegisterAsync(User user, Household household, HouseholdMember membership, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
