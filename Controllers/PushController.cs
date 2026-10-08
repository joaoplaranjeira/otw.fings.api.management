using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Notifications;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("push-subscriptions")]
[Route("api/push")]
public sealed class PushController(
    INotificationManagementService service,
    ICurrentUserContext currentUser,
    IOptions<WebPushSettings> webPush) : ControllerBase
{
    [HttpGet("public-key")]
    public IActionResult GetPublicKey() => Ok(new { publicKey = webPush.Value.PublicKey });

    [HttpPut("subscriptions")]
    public async Task<ActionResult<PushSubscriptionResponse>> Put(PushSubscriptionRequest request, CancellationToken cancellationToken) =>
        Ok(await service.PutSubscriptionAsync(request, currentUser.UserId, Request.Headers.UserAgent.ToString(), cancellationToken));

    [HttpGet("subscriptions")]
    public async Task<ActionResult<IReadOnlyList<PushSubscriptionResponse>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetSubscriptionsAsync(currentUser.UserId, cancellationToken));

    [HttpDelete("subscriptions/{subscriptionId:guid}")]
    public async Task<IActionResult> Delete(Guid subscriptionId, CancellationToken cancellationToken)
    {
        await service.DeleteSubscriptionAsync(subscriptionId, currentUser.UserId, cancellationToken);
        return NoContent();
    }
}
