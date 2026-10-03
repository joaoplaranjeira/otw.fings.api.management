using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/expenses")]
public sealed class ExpensesController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseResponse>>> GetAll(
        Guid householdId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await service.GetExpensesAsync(householdId, currentUser.UserId, from, to, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create(
        Guid householdId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var expense = await service.CreateExpenseAsync(householdId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/expenses/{expense.Id}", expense);
    }

    [HttpPatch("{expenseId:guid}")]
    public async Task<ActionResult<ExpenseResponse>> Update(
        Guid householdId,
        Guid expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateExpenseAsync(householdId, expenseId, currentUser.UserId, request, cancellationToken));

    [HttpPut("{expenseId:guid}/lines")]
    public async Task<ActionResult<ExpenseResponse>> ReplaceLines(
        Guid householdId,
        Guid expenseId,
        ReplaceExpenseLinesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ReplaceExpenseLinesAsync(householdId, expenseId, currentUser.UserId, request, cancellationToken));
}
