using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Notifications;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/notification-rules")]
public sealed class NotificationRulesController(INotificationManagementService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationRuleResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetRulesAsync(currentUser.UserId, cancellationToken));

    [HttpGet("{ruleId:guid}")]
    public async Task<ActionResult<NotificationRuleResponse>> Get(Guid ruleId, CancellationToken cancellationToken) =>
        Ok(await service.GetRuleAsync(ruleId, currentUser.UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<NotificationRuleResponse>> Create(NotificationRuleRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateRuleAsync(request, currentUser.UserId, cancellationToken);
        return Created($"/api/notification-rules/{result.Id}", result);
    }

    [HttpPatch("{ruleId:guid}")]
    public async Task<ActionResult<NotificationRuleResponse>> Update(Guid ruleId, NotificationRuleRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateRuleAsync(ruleId, request, currentUser.UserId, cancellationToken));

    [HttpDelete("{ruleId:guid}")]
    public async Task<IActionResult> Delete(Guid ruleId, CancellationToken cancellationToken)
    {
        await service.DisableRuleAsync(ruleId, currentUser.UserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ruleId:guid}/test")]
    public async Task<ActionResult<NotificationRuleTestResponse>> Test(Guid ruleId, NotificationRuleTestRequest request, CancellationToken cancellationToken) =>
        Ok(await service.TestRuleAsync(ruleId, request, currentUser.UserId, cancellationToken));
}
