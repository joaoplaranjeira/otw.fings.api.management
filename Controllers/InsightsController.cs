using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/insights")]
public sealed class InsightsController(IInsightsService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("{year:int}/{month:int}")]
    public async Task<ActionResult<HouseholdInsightsResponse>> GetAll(
        Guid householdId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            return ValidationProblem("O ano e o mês indicados não são válidos.");
        }

        return Ok(await service.GetInsightsAsync(
            householdId,
            currentUser.UserId,
            new DateOnly(year, month, 1),
            cancellationToken));
    }
}
