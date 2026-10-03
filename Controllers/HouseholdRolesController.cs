using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/household-roles")]
public sealed class HouseholdRolesController(IFinanceService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<HouseholdRoleResponse>> GetAll() =>
        Ok(service.GetHouseholdRoles());
}
