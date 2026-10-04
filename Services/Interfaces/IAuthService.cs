namespace otw.fings.api.management.Services.Interfaces;

public interface IAuthService
{
    Task<OtpResponse> SendOtpAsync(string email, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<OtpResponse> ValidateOtpAsync(string email, string code, CancellationToken cancellationToken);
    Task<UserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> GetUserAsync(long userId, CancellationToken cancellationToken);
    Task<UserResponse> UpdateProfileAsync(long userId, UpdateUserProfileRequest request, CancellationToken cancellationToken);
}

public interface ITokenService
{
    string GenerateToken(User user, IEnumerable<string> permissions);
}

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string email, string otpCode, CancellationToken cancellationToken);
    Task<bool> SendHouseholdInvitationEmailAsync(
        string email,
        string householdName,
        string role,
        string invitationCode,
        string invitationUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}
