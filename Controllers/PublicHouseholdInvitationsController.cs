using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Route("api/household-invitations")]
public sealed class PublicHouseholdInvitationsController(
    IHouseholdInvitationService service,
    ICurrentUserContext currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{code}")]
    public async Task<ActionResult<HouseholdInvitationPreviewResponse>> GetPreview(
        string code,
        CancellationToken cancellationToken) =>
        Ok(await service.GetPreviewAsync(code, cancellationToken));

    [Authorize]
    [HttpPost("{code}/accept")]
    public async Task<ActionResult<HouseholdMemberResponse>> Accept(
        string code,
        CancellationToken cancellationToken) =>
        Ok(await service.AcceptAsync(code, currentUser.UserId, cancellationToken));
}
