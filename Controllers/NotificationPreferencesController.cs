using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Notifications;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Authorize]
[Route("api/notification-preferences")]
public sealed class NotificationPreferencesController(INotificationManagementService service, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceItem>>> Get(CancellationToken cancellationToken) =>
        Ok(await service.GetPreferencesAsync(currentUser.UserId, cancellationToken));

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<NotificationPreferenceItem>>> Put(PutNotificationPreferencesRequest request, CancellationToken cancellationToken) =>
        Ok(await service.PutPreferencesAsync(request, currentUser.UserId, cancellationToken));
}
