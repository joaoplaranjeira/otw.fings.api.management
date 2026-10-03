using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/members")]
public sealed class HouseholdMembersController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HouseholdMemberResponse>>> GetAll(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetHouseholdMembersAsync(householdId, currentUser.UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<HouseholdMemberResponse>> Create(
        Guid householdId,
        AddHouseholdMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await service.AddHouseholdMemberAsync(
            householdId,
            currentUser.UserId,
            request,
            cancellationToken);
        return Created($"/api/households/{householdId}/members/{member.Id}", member);
    }

    [HttpDelete("{memberId:guid}")]
    public async Task<IActionResult> Delete(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await service.RemoveHouseholdMemberAsync(householdId, memberId, currentUser.UserId, cancellationToken);
        return NoContent();
    }
}
