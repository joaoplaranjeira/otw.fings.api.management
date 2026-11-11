using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/product-types")]
public sealed class ProductTypesController(IFinanceService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductTypeResponse>>> GetAll(
        Guid householdId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetProductTypesAsync(householdId, currentUser.UserId, includeInactive, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProductTypeResponse>> Create(
        Guid householdId,
        CreateProductTypeRequest request,
        CancellationToken cancellationToken)
    {
        var productType = await service.CreateProductTypeAsync(householdId, currentUser.UserId, request, cancellationToken);
        return Created($"/api/households/{householdId}/product-types/{productType.Id}", productType);
    }

    [HttpPatch("{productTypeId:guid}")]
    public async Task<ActionResult<ProductTypeResponse>> Update(
        Guid householdId,
        Guid productTypeId,
        UpdateProductTypeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateProductTypeAsync(
            householdId, productTypeId, currentUser.UserId, request, cancellationToken));
}
