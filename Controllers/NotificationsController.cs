using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Notifications;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationManagementService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> Get([FromQuery] bool unreadOnly, CancellationToken cancellationToken) =>
        Ok(await service.GetNotificationsAsync(currentUser.UserId, unreadOnly, cancellationToken));

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        await service.MarkReadAsync(notificationId, currentUser.UserId, cancellationToken);
        return NoContent();
    }
}
