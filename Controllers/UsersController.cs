using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Security;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IAuthService service, ICurrentUserContext currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Register(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var user = await service.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCurrent), user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetCurrent(CancellationToken cancellationToken)
    {
        var user = await service.GetUserAsync(currentUser.UserId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [Authorize]
    [HttpPatch("me")]
    public async Task<ActionResult<UserResponse>> UpdateCurrent(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateProfileAsync(currentUser.UserId, request, cancellationToken));
}
