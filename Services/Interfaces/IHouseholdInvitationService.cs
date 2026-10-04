namespace otw.fings.api.management.Services.Interfaces;

public interface IHouseholdInvitationService
{
    Task<HouseholdInvitationCreatedResponse> CreateAsync(
        Guid householdId,
        long userId,
        CreateHouseholdInvitationRequest request,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<HouseholdInvitationResponse>> GetAllAsync(
        Guid householdId,
        long userId,
        CancellationToken cancellationToken);
    Task<HouseholdInvitationPreviewResponse> GetPreviewAsync(string code, CancellationToken cancellationToken);
    Task<HouseholdMemberResponse> AcceptAsync(string code, long userId, CancellationToken cancellationToken);
    Task RevokeAsync(Guid householdId, Guid invitationId, long userId, CancellationToken cancellationToken);
    Task<HouseholdInvitationEmailResponse> SendEmailAsync(
        Guid householdId,
        Guid invitationId,
        long userId,
        CancellationToken cancellationToken);
    Task<HouseholdInvitationCreatedResponse> RegenerateCodeAsync(
        Guid householdId,
        Guid invitationId,
        long userId,
        CancellationToken cancellationToken);
}
