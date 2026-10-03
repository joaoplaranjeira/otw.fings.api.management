using Microsoft.AspNetCore.Mvc;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Controllers;

[ApiController]
[Route("api/otp")]
public sealed class OtpController(IAuthService service) : ControllerBase
{
    [HttpPost("send")]
    public async Task<ActionResult<OtpResponse>> Send(SendOtpRequest request, CancellationToken cancellationToken)
    {
        var response = await service.SendOtpAsync(
            request.Email,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("validate")]
    public async Task<ActionResult<OtpResponse>> Validate(ValidateOtpRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ValidateOtpAsync(request.Email, request.Code, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
