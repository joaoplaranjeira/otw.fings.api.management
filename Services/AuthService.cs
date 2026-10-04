using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Domain.Catalogs;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Services;

public sealed class AuthService(
    IAuthRepository repository,
    IEmailService emailService,
    ITokenService tokenService,
    IOptions<OtpSettings> otpOptions) : IAuthService
{
    private const string BypassCode = "123123";
    private const int OtpExpirationMinutes = 10;
    private const int MaxFailedAttempts = 5;
    private const int BlockDurationMinutes = 15;
    private const int RateLimitRequests = 3;
    private const int RateLimitWindowMinutes = 15;

    public async Task<OtpResponse> SendOtpAsync(
        string email,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = Normalize(email);
        if (otpOptions.Value.BypassEnabled)
        {
            return new(true, "Se o email estiver registado e ativo, poderá iniciar sessão.");
        }

        var user = await repository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return new(true, "Se o email estiver registado e ativo, receberá um código OTP.");
        }

        var latest = await repository.GetLatestOtpAsync(normalizedEmail, cancellationToken);
        if (latest?.BlockedUntil > DateTime.UtcNow)
        {
            return new(false, "Demasiadas tentativas. Tente novamente mais tarde.");
        }

        var recentCount = await repository.CountRecentOtpsAsync(
            normalizedEmail,
            DateTime.UtcNow.AddMinutes(-RateLimitWindowMinutes),
            cancellationToken);
        if (recentCount >= RateLimitRequests)
        {
            return new(false, "Foram efetuados demasiados pedidos. Aguarde antes de pedir outro código.");
        }

        var now = DateTime.UtcNow;
        var otp = new OtpCode
        {
            Email = normalizedEmail,
            Code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(OtpExpirationMinutes),
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        await repository.AddOtpAsync(otp, cancellationToken);

        if (!await emailService.SendOtpEmailAsync(normalizedEmail, otp.Code, cancellationToken))
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
            await repository.SaveChangesAsync(cancellationToken);
            return new(false, "Não foi possível enviar o email OTP.");
        }

        return new(true, "OTP enviado com sucesso.", otp.ExpiresAt);
    }

    public async Task<OtpResponse> ValidateOtpAsync(string email, string code, CancellationToken cancellationToken)
    {
        var normalizedEmail = Normalize(email);
        var user = await repository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (otpOptions.Value.BypassEnabled)
        {
            if (user is null || !user.IsActive || !CodesMatch(BypassCode, code))
            {
                return InvalidOtp();
            }

            var bypassPermissions = await repository.GetPermissionsAsync(user.Id, cancellationToken);
            return new(true, "Autenticação concluída com sucesso.", Token: tokenService.GenerateToken(user, bypassPermissions));
        }

        var otp = await repository.GetLatestOtpAsync(normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive || otp is null)
        {
            return InvalidOtp();
        }

        if (otp.BlockedUntil > DateTime.UtcNow)
        {
            return new(false, "Conta temporariamente bloqueada.");
        }
        if (otp.IsUsed)
        {
            return new(false, "O código OTP já foi utilizado.");
        }
        if (DateTime.UtcNow > otp.ExpiresAt)
        {
            return new(false, "O código OTP expirou.");
        }

        if (!CodesMatch(otp.Code, code))
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= MaxFailedAttempts)
            {
                otp.BlockedUntil = DateTime.UtcNow.AddMinutes(BlockDurationMinutes);
            }
            await repository.SaveChangesAsync(cancellationToken);
            return InvalidOtp();
        }

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
        var permissions = await repository.GetPermissionsAsync(user.Id, cancellationToken);
        return new(true, "Autenticação concluída com sucesso.", Token: tokenService.GenerateToken(user, permissions));
    }

    public async Task<UserResponse> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var username = Normalize(request.Username);
        var householdName = request.HouseholdName?.Trim();
        var invitationCode = request.InvitationCode?.Trim();
        if (!string.IsNullOrWhiteSpace(householdName) && !string.IsNullOrWhiteSpace(invitationCode))
        {
            throw new ValidationException("Não pode criar um agregado e aceitar um convite no mesmo registo.");
        }

        if (await repository.GetUserByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("Já existe um utilizador com este email.");
        }
        if (await repository.GetUserByUsernameAsync(username, cancellationToken) is not null)
        {
            throw new ConflictException("Já existe um utilizador com este username.");
        }

        HouseholdInvitation? invitation = null;
        if (!string.IsNullOrWhiteSpace(invitationCode))
        {
            invitation = await repository.GetHouseholdInvitationByCodeHashAsync(
                HouseholdInvitationCodes.Hash(invitationCode),
                cancellationToken) ?? throw new NotFoundException("Convite não encontrado.");
            EnsureInvitationCanBeAccepted(invitation, email);
        }

        var now = DateTime.UtcNow;
        var nowOffset = new DateTimeOffset(now, TimeSpan.Zero);
        var user = new User
        {
            Name = request.Name.Trim(),
            Username = username,
            Email = email,
            Role = invitation?.Role.ToString() ?? (!string.IsNullOrWhiteSpace(householdName) ? "Owner" : "User"),
            Profile = "Utilizador",
            InsertedUser = email,
            InsertedDate = now,
            UpdatedDate = now
        };
        Household? household = null;
        HouseholdMember? membership = null;
        IReadOnlyList<Category>? categories = null;

        if (!string.IsNullOrWhiteSpace(householdName))
        {
            household = new Household { Name = householdName };
            membership = new HouseholdMember { HouseholdId = household.Id, Role = HouseholdRole.Owner };
            categories = StandardCategoryCatalog.CreateFor(household.Id);
        }
        else if (invitation is not null)
        {
            membership = new HouseholdMember
            {
                HouseholdId = invitation.HouseholdId,
                Role = invitation.Role
            };
        }

        await repository.RegisterAsync(
            user,
            household,
            membership,
            categories,
            invitation,
            invitation is null ? null : nowOffset,
            cancellationToken);
        return Map(user);
    }

    public async Task<UserResponse?> GetUserAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await repository.GetUserByIdAsync(userId, cancellationToken);
        return user is null ? null : Map(user);
    }

    public async Task<UserResponse> UpdateProfileAsync(
        long userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Name is null && request.Username is null)
        {
            throw new ValidationException("Indique pelo menos um campo para atualizar.");
        }

        string? name = null;
        if (request.Name is not null)
        {
            name = request.Name.Trim();
            if (name.Length == 0) throw new ValidationException("O nome não pode estar vazio.");
        }

        string? username = null;
        if (request.Username is not null)
        {
            username = Normalize(request.Username);
            if (username.Length == 0) throw new ValidationException("O username não pode estar vazio.");

            var existingUser = await repository.GetUserByUsernameAsync(username, cancellationToken);
            if (existingUser is not null && existingUser.Id != userId)
            {
                throw new ConflictException("Já existe um utilizador com este username.");
            }
        }

        var user = await repository.UpdateUserProfileAsync(userId, name, username, cancellationToken)
            ?? throw new NotFoundException("Utilizador não encontrado.");
        return Map(user);
    }

    private static UserResponse Map(User user) => new(user.Id, user.Name, user.Username, user.Email, user.IsActive);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static void EnsureInvitationCanBeAccepted(HouseholdInvitation invitation, string email)
    {
        if (invitation.Status == HouseholdInvitationStatus.Accepted)
        {
            throw new ConflictException("O convite já foi aceite.");
        }
        if (invitation.Status == HouseholdInvitationStatus.Revoked ||
            invitation.Status == HouseholdInvitationStatus.Expired ||
            invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new GoneException("O convite expirou ou foi revogado.");
        }
        if (!string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("O email não corresponde ao destinatário do convite.");
        }
    }
    private static bool CodesMatch(string expectedCode, string suppliedCode)
    {
        var expected = Encoding.UTF8.GetBytes(expectedCode);
        var supplied = Encoding.UTF8.GetBytes(suppliedCode);
        return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static OtpResponse InvalidOtp() => new(false, "Email ou código OTP inválido.");
}
