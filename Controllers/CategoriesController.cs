using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/categories")]
public sealed class CategoriesController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(Guid householdId, CancellationToken cancellationToken) =>
        Ok(await service.GetCategoriesAsync(householdId, currentUser.UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(
        Guid householdId,
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await service.CreateCategoryAsync(householdId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/categories/{category.Id}", category);
    }

    [HttpPatch("{categoryId:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid householdId,
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateCategoryAsync(householdId, categoryId, currentUser.UserId, request, cancellationToken));

    [HttpPost("{categoryId:guid}/subcategories")]
    public async Task<ActionResult<SubcategoryResponse>> CreateSubcategory(
        Guid householdId,
        Guid categoryId,
        CreateSubcategoryRequest request,
        CancellationToken cancellationToken)
    {
        var subcategory = await service.CreateSubcategoryAsync(householdId, categoryId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/categories/{categoryId}/subcategories/{subcategory.Id}", subcategory);
    }

    [HttpPatch("{categoryId:guid}/subcategories/{subcategoryId:guid}")]
    public async Task<ActionResult<SubcategoryResponse>> UpdateSubcategory(
        Guid householdId,
        Guid categoryId,
        Guid subcategoryId,
        UpdateSubcategoryRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateSubcategoryAsync(
            householdId,
            categoryId,
            subcategoryId,
            currentUser.UserId,
            request,
            cancellationToken));
}
