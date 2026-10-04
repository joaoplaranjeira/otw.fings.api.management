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

        return await SendAsync(
            apiKey,
            transactionalId,
            email,
            new { otpCode },
            "OTP",
            cancellationToken);
    }

    public async Task<bool> SendHouseholdInvitationEmailAsync(
        string email,
        string householdName,
        string role,
        string invitationCode,
        string invitationUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["Loops:ApiKey"];
        var transactionalId = configuration["Loops:InvitationTransactionalId"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(transactionalId))
        {
            logger.LogError("Loops household invitation email configuration is incomplete.");
            return false;
        }

        return await SendAsync(
            apiKey,
            transactionalId,
            email,
            new
            {
                householdName,
                role,
                invitationCode,
                invitationUrl,
                expiresAt = expiresAt.ToString("yyyy-MM-dd HH:mm 'UTC'")
            },
            "household invitation",
            cancellationToken);
    }

    private async Task<bool> SendAsync(
        string apiKey,
        string transactionalId,
        string email,
        object dataVariables,
        string emailType,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://app.loops.so/api/v1/transactional");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = JsonContent.Create(new { transactionalId, email, dataVariables });
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Loops failed to send a {EmailType} email. Status: {StatusCode}", emailType, response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Loops request failed while sending a {EmailType} email.", emailType);
            return false;
        }
    }
}
