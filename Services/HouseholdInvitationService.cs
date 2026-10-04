using Microsoft.Extensions.Options;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Services;

public sealed class HouseholdInvitationService(
    IHouseholdInvitationRepository invitationRepository,
    IFinanceRepository financeRepository,
    IAuthRepository authRepository,
    IEmailService emailService,
    IOptions<HouseholdInvitationSettings> options) : IHouseholdInvitationService
{
    public async Task<HouseholdInvitationCreatedResponse> CreateAsync(
        Guid householdId,
        long userId,
        CreateHouseholdInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var requesterRole = await EnsureCanManageAsync(householdId, userId, cancellationToken);
        ValidateAssignableRole(requesterRole, request.Role);

        var household = await invitationRepository.GetHouseholdAsync(householdId, cancellationToken)
            ?? throw new NotFoundException("Agregado não encontrado.");
        var email = NormalizeEmail(request.Email);
        var existingUser = await financeRepository.GetActiveUserByEmailAsync(email, cancellationToken);
        if (existingUser is not null &&
            await financeRepository.HasHouseholdMemberAsync(householdId, existingUser.Id, cancellationToken))
        {
            throw new ConflictException("O utilizador já pertence a este agregado.");
        }

        var now = DateTimeOffset.UtcNow;
        if (await invitationRepository.HasPendingInvitationAsync(householdId, email, now, cancellationToken))
        {
            throw new ConflictException("Já existe um convite pendente para este email.");
        }

        var code = await GenerateUniqueCodeAsync(cancellationToken);
        var invitation = new HouseholdInvitation
        {
            HouseholdId = householdId,
            Household = household,
            Email = email,
            Role = request.Role,
            Code = code,
            CodeHash = HouseholdInvitationCodes.Hash(code),
            Status = HouseholdInvitationStatus.Pending,
            ExpiresAtUtc = now.AddDays(options.Value.ExpirationDays),
            CreatedByUserId = userId
        };
        await invitationRepository.AddAsync(invitation, cancellationToken);

        var url = BuildInvitationUrl(code);
        return MapCreated(invitation, code, url);
    }

    public async Task<IReadOnlyList<HouseholdInvitationResponse>> GetAllAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(householdId, userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return (await invitationRepository.GetAllAsync(householdId, cancellationToken))
            .Select(invitation => Map(invitation, EffectiveStatus(invitation, now)))
            .ToArray();
    }

    public async Task<HouseholdInvitationPreviewResponse> GetPreviewAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var invitation = await GetInvitationByCodeAsync(code, cancellationToken);
        EnsurePending(invitation);
        return new(
            invitation.Household.Name,
            MaskEmail(invitation.Email),
            invitation.Role,
            invitation.ExpiresAtUtc);
    }

    public async Task<HouseholdMemberResponse> AcceptAsync(
        string code,
        long userId,
        CancellationToken cancellationToken)
    {
        var invitation = await GetInvitationByCodeAsync(code, cancellationToken);
        EnsurePending(invitation);
        var user = await authRepository.GetUserByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Utilizador não encontrado.");
        if (!user.IsActive)
        {
            throw new ForbiddenException("O utilizador não está ativo.");
        }
        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("O email da conta não corresponde ao destinatário do convite.");
        }
        if (await financeRepository.HasHouseholdMemberAsync(invitation.HouseholdId, userId, cancellationToken))
        {
            throw new ConflictException("O utilizador já pertence a este agregado.");
        }

        var member = new HouseholdMember
        {
            HouseholdId = invitation.HouseholdId,
            UserId = userId,
            Role = invitation.Role
        };
        await invitationRepository.AcceptAsync(invitation, member, DateTimeOffset.UtcNow, cancellationToken);
        return new(member.Id, user.Id, user.Name, user.Username, user.Email, user.IsActive, member.Role);
    }

    public async Task RevokeAsync(
        Guid householdId,
        Guid invitationId,
        long userId,
        CancellationToken cancellationToken)
    {
        var requesterRole = await EnsureCanManageAsync(householdId, userId, cancellationToken);
        var invitation = await invitationRepository.GetAsync(householdId, invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");
        ValidateCanManageInvitation(requesterRole, invitation.Role);
        EnsurePending(invitation);
        invitation.Status = HouseholdInvitationStatus.Revoked;
        await invitationRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<HouseholdInvitationCreatedResponse> RegenerateCodeAsync(
        Guid householdId,
        Guid invitationId,
        long userId,
        CancellationToken cancellationToken)
    {
        var requesterRole = await EnsureCanManageAsync(householdId, userId, cancellationToken);
        var invitation = await invitationRepository.GetAsync(householdId, invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");
        ValidateCanManageInvitation(requesterRole, invitation.Role);
        if (invitation.Status == HouseholdInvitationStatus.Accepted)
        {
            throw new ConflictException("O convite já foi aceite.");
        }
        if (invitation.Status == HouseholdInvitationStatus.Revoked)
        {
            throw new GoneException("O convite foi revogado.");
        }

        var code = await GenerateUniqueCodeAsync(cancellationToken);
        invitation.Code = code;
        invitation.CodeHash = HouseholdInvitationCodes.Hash(code);
        invitation.Status = HouseholdInvitationStatus.Pending;
        invitation.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(options.Value.ExpirationDays);
        invitation.EmailSent = false;
        await invitationRepository.SaveChangesAsync(cancellationToken);

        var url = BuildInvitationUrl(code);
        return MapCreated(invitation, code, url);
    }

    public async Task<HouseholdInvitationEmailResponse> SendEmailAsync(
        Guid householdId,
        Guid invitationId,
        long userId,
        CancellationToken cancellationToken)
    {
        var requesterRole = await EnsureCanManageAsync(householdId, userId, cancellationToken);
        var invitation = await invitationRepository.GetAsync(householdId, invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");
        ValidateCanManageInvitation(requesterRole, invitation.Role);
        EnsurePending(invitation);

        invitation.EmailSent = await emailService.SendHouseholdInvitationEmailAsync(
            invitation.Email,
            invitation.Household.Name,
            invitation.Role.ToString(),
            invitation.Code,
            BuildInvitationUrl(invitation.Code),
            invitation.ExpiresAtUtc,
            cancellationToken);
        await invitationRepository.SaveChangesAsync(cancellationToken);
        return new(invitation.Id, invitation.Email, invitation.EmailSent);
    }

    private async Task<HouseholdRole> EnsureCanManageAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken)
    {
        var role = await financeRepository.GetMemberRoleAsync(householdId, userId, cancellationToken)
            ?? throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        if (role is not (HouseholdRole.Owner or HouseholdRole.Administrator))
        {
            throw new ForbiddenException("O utilizador não pode gerir convites deste agregado.");
        }
        return role;
    }

    private static void ValidateAssignableRole(HouseholdRole requesterRole, HouseholdRole assignedRole)
    {
        if (!Enum.IsDefined(assignedRole))
        {
            throw new ValidationException("O papel indicado não é válido.");
        }
        ValidateCanManageInvitation(requesterRole, assignedRole);
    }

    private static void ValidateCanManageInvitation(HouseholdRole requesterRole, HouseholdRole invitationRole)
    {
        if (requesterRole == HouseholdRole.Administrator &&
            invitationRole is HouseholdRole.Owner or HouseholdRole.Administrator)
        {
            throw new ForbiddenException("Um administrador só pode gerir convites para Member ou Viewer.");
        }
    }

    private async Task<HouseholdInvitation> GetInvitationByCodeAsync(
        string code,
        CancellationToken cancellationToken) =>
        await invitationRepository.GetByCodeHashAsync(HouseholdInvitationCodes.Hash(code), cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");

    private static void EnsurePending(HouseholdInvitation invitation)
    {
        if (invitation.Status == HouseholdInvitationStatus.Accepted)
        {
            throw new ConflictException("O convite já foi aceite.");
        }
        if (invitation.Status is HouseholdInvitationStatus.Revoked or HouseholdInvitationStatus.Expired ||
            invitation.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            throw new GoneException("O convite expirou ou foi revogado.");
        }
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = HouseholdInvitationCodes.Generate();
            if (await invitationRepository.GetByCodeHashAsync(
                    HouseholdInvitationCodes.Hash(code), cancellationToken) is null)
            {
                return code;
            }
        }
        throw new InvalidOperationException("Não foi possível gerar um código de convite único.");
    }

    private string BuildInvitationUrl(string code) =>
        $"{options.Value.FrontendBaseUrl.TrimEnd('/')}/register?invitationCode={Uri.EscapeDataString(code)}";

    private static HouseholdInvitationStatus EffectiveStatus(
        HouseholdInvitation invitation,
        DateTimeOffset now) =>
        invitation.Status == HouseholdInvitationStatus.Pending && invitation.ExpiresAtUtc <= now
            ? HouseholdInvitationStatus.Expired
            : invitation.Status;

    private static HouseholdInvitationCreatedResponse MapCreated(
        HouseholdInvitation invitation,
        string code,
        string invitationUrl) =>
        new(
            invitation.Id,
            invitation.HouseholdId,
            invitation.Household.Name,
            invitation.Email,
            invitation.Role,
            code,
            invitationUrl,
            invitation.EmailSent,
            invitation.Status,
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc,
            invitation.AcceptedAtUtc);

    private static HouseholdInvitationResponse Map(
        HouseholdInvitation invitation,
        HouseholdInvitationStatus status) =>
        new(
            invitation.Id,
            invitation.HouseholdId,
            invitation.Household.Name,
            invitation.Email,
            invitation.Role,
            invitation.EmailSent,
            status,
            invitation.ExpiresAtUtc,
            invitation.CreatedAtUtc,
            invitation.AcceptedAtUtc);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string MaskEmail(string email)
    {
        var separator = email.IndexOf('@');
        if (separator <= 0) return "***";
        var local = email[..separator];
        var visible = local.Length == 1 ? local : $"{local[0]}***{local[^1]}";
        return $"{visible}{email[separator..]}";
    }
}
