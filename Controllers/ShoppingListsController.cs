using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/shopping-lists")]
public sealed class ShoppingListsController(
    IShoppingListService service,
    ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShoppingListResponse>>> GetLists(
        Guid householdId,
        [FromQuery] bool activeOnly = true,
        [FromQuery] bool includeCompletedItems = false,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetListsAsync(
            householdId, currentUser.UserId, activeOnly, includeCompletedItems, cancellationToken));

    [HttpGet("{listId:guid}")]
    public async Task<ActionResult<ShoppingListResponse>> GetList(
        Guid householdId,
        Guid listId,
        [FromQuery] bool includeCompletedItems = false,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetListAsync(
            householdId, listId, currentUser.UserId, includeCompletedItems, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ShoppingListResponse>> CreateList(
        Guid householdId,
        CreateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateListAsync(householdId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/shopping-lists/{result.Id}", result);
    }

    [HttpPatch("{listId:guid}")]
    public async Task<ActionResult<ShoppingListResponse>> UpdateList(
        Guid householdId,
        Guid listId,
        UpdateShoppingListRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateListAsync(householdId, listId, currentUser.UserId, request, cancellationToken));

    [HttpDelete("{listId:guid}")]
    public async Task<IActionResult> ArchiveList(
        Guid householdId,
        Guid listId,
        CancellationToken cancellationToken)
    {
        await service.ArchiveListAsync(householdId, listId, currentUser.UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{listId:guid}/items")]
    public async Task<ActionResult<ShoppingListItemResponse>> AddItem(
        Guid householdId,
        Guid listId,
        AddShoppingListItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AddItemAsync(householdId, listId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/shopping-lists/{listId}/items/{result.Id}", result);
    }

    [HttpPost("{listId:guid}/items/from-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<AddShoppingListItemFromImageResponse>> AddItemFromImage(
        Guid householdId,
        Guid listId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await service.AddItemFromImageAsync(
            householdId,
            listId,
            currentUser.UserId,
            stream,
            file.ContentType,
            file.Length,
            cancellationToken);
        return Created($"/api/households/{householdId}/shopping-lists/{listId}/items/{result.Item.Id}", result);
    }

    [HttpPatch("{listId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ShoppingListItemResponse>> UpdateItem(
        Guid householdId,
        Guid listId,
        Guid itemId,
        UpdateShoppingListItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateItemAsync(
            householdId, listId, itemId, currentUser.UserId, request, cancellationToken));

    [HttpDelete("{listId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(
        Guid householdId,
        Guid listId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        await service.RemoveItemAsync(householdId, listId, itemId, currentUser.UserId, cancellationToken);
        return NoContent();
    }

    [HttpGet("expense-matches")]
    public async Task<ActionResult<IReadOnlyList<ShoppingListExpenseMatchResponse>>> GetExpenseMatches(
        Guid householdId,
        [FromQuery] ShoppingListMatchStatus? status = ShoppingListMatchStatus.Pending,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetExpenseMatchesAsync(householdId, currentUser.UserId, status, cancellationToken));

    [HttpPatch("expense-matches/{matchId:guid}")]
    public async Task<ActionResult<ShoppingListExpenseMatchResponse>> ReviewExpenseMatch(
        Guid householdId,
        Guid matchId,
        ReviewShoppingListExpenseMatchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ReviewExpenseMatchAsync(
            householdId, matchId, currentUser.UserId, request, cancellationToken));
}
