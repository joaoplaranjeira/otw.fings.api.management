using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/invitations")]
public sealed class HouseholdInvitationsController(
    IHouseholdInvitationService service,
    ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<HouseholdInvitationCreatedResponse>> Create(
        Guid householdId,
        CreateHouseholdInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var invitation = await service.CreateAsync(
            householdId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Created($"/api/households/{householdId}/invitations/{invitation.Id}", invitation);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HouseholdInvitationResponse>>> GetAll(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(householdId, currentUser.UserId, cancellationToken));

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid householdId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await service.RevokeAsync(householdId, invitationId, currentUser.UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{invitationId:guid}/regenerate-code")]
    public async Task<ActionResult<HouseholdInvitationCreatedResponse>> RegenerateCode(
        Guid householdId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        Ok(await service.RegenerateCodeAsync(householdId, invitationId, currentUser.UserId, cancellationToken));

    [HttpPost("{invitationId:guid}/send-email")]
    public async Task<ActionResult<HouseholdInvitationEmailResponse>> SendEmail(
        Guid householdId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        Ok(await service.SendEmailAsync(householdId, invitationId, currentUser.UserId, cancellationToken));
}
