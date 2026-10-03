using System.Net.Http.Headers;
using System.Net.Http.Json;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Services;

public sealed class LoopsEmailService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<LoopsEmailService> logger) : IEmailService
{
    public async Task<bool> SendOtpEmailAsync(string email, string otpCode, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Loops:ApiKey"];
        var transactionalId = configuration["Loops:TransactionalId"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(transactionalId))
        {
            logger.LogError("Loops email configuration is incomplete.");
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://app.loops.so/api/v1/transactional");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new
        {
            transactionalId,
            email,
            dataVariables = new { otpCode }
        });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Loops failed to send an OTP email. Status: {StatusCode}", response.StatusCode);
        }

        return response.IsSuccessStatusCode;
    }
}
