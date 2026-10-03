using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/recurring-expenses")]
public sealed class RecurringExpensesController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringExpenseResponse>>> GetAll(Guid householdId, CancellationToken cancellationToken) =>
        Ok(await service.GetRecurringExpensesAsync(householdId, currentUser.UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RecurringExpenseResponse>> Create(
        Guid householdId,
        CreateRecurringExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var recurring = await service.CreateRecurringExpenseAsync(householdId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/recurring-expenses/{recurring.Id}", recurring);
    }

    [HttpPatch("{recurringExpenseId:guid}")]
    public async Task<ActionResult<RecurringExpenseResponse>> Update(
        Guid householdId,
        Guid recurringExpenseId,
        UpdateRecurringExpenseRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateRecurringExpenseAsync(
            householdId,
            recurringExpenseId,
            currentUser.UserId,
            request,
            cancellationToken));

    [HttpPost("{recurringExpenseId:guid}/materialize")]
    public async Task<ActionResult<RecurringExpenseMaterializationResponse>> Materialize(
        Guid householdId,
        Guid recurringExpenseId,
        CancellationToken cancellationToken) =>
        Ok(await service.MaterializeRecurringExpenseAsync(
            householdId,
            recurringExpenseId,
            currentUser.UserId,
            cancellationToken));
}
