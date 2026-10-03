using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}")]
public sealed class BudgetsController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet("budgets")]
    public async Task<ActionResult<IReadOnlyList<BudgetPeriodResponse>>> GetBudgets(Guid householdId, CancellationToken cancellationToken) =>
        Ok(await service.GetBudgetsAsync(householdId, currentUser.UserId, cancellationToken));

    [HttpPost("budgets")]
    public async Task<ActionResult<BudgetPeriodResponse>> CreateBudget(
        Guid householdId,
        CreateBudgetPeriodRequest request,
        CancellationToken cancellationToken)
    {
        var budget = await service.CreateBudgetAsync(householdId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/budgets/{budget.Id}", budget);
    }

    [HttpGet("incomes")]
    public async Task<ActionResult<IReadOnlyList<IncomeResponse>>> GetIncomes(
        Guid householdId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await service.GetIncomesAsync(householdId, currentUser.UserId, from, to, cancellationToken));

    [HttpGet("dashboard/{year:int}/{month:int}")]
    public async Task<ActionResult<BudgetDashboardResponse>> GetDashboard(
        Guid householdId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12) return ValidationProblem("O mês deve estar entre 1 e 12.");
        return Ok(await service.GetDashboardAsync(householdId, currentUser.UserId, new DateOnly(year, month, 1), cancellationToken));
    }
}
