using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/households/{householdId:guid}/receipts")]
public sealed class ReceiptsController(
    IReceiptParser parser,
    IReceiptParseReviewService reviewService,
    ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost("parse")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ReceiptParseResponse>> Parse(
        Guid householdId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await parser.ParseAsync(
            householdId,
            currentUser.UserId,
            stream,
            file.ContentType,
            file.Length,
            cancellationToken));
    }

    [HttpPatch("parses/{parseId:guid}/validation")]
    public async Task<ActionResult<ReceiptParseHistoryItemResponse>> Validate(
        Guid householdId,
        Guid parseId,
        ValidateReceiptParseRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reviewService.ValidateAsync(
            householdId,
            parseId,
            currentUser.UserId,
            request,
            cancellationToken));

    [HttpGet("parses/history")]
    public async Task<ActionResult<ReceiptParseQualityHistoryResponse>> GetHistory(
        Guid householdId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await reviewService.GetHistoryAsync(householdId, currentUser.UserId, limit, cancellationToken));
}
