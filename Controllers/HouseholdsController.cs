using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households")]
public sealed class HouseholdsController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HouseholdResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetHouseholdsAsync(currentUser.UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<HouseholdResponse>> Create(CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
        var household = await service.CreateHouseholdAsync(currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{household.Id}", household);
    }
}
