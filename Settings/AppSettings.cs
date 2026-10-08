using System.ComponentModel.DataAnnotations;

namespace otw.fings.api.management.Settings;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;
}

public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; init; } = string.Empty;
    public string ExtractionModel { get; init; } = "gpt-4o";
    public string Model { get; init; } = "gpt-4o-mini";
    public bool VerifyReceiptValues { get; init; }
    [Range(1, 100)]
    public int MinimumReceiptImageQuality { get; init; } = 80;
    public int MaximumReceiptBytes { get; init; } = 10 * 1024 * 1024;
}

public sealed class OtpSettings
{
    public const string SectionName = "Otp";

    public bool BypassEnabled { get; init; }
}

public sealed class HouseholdInvitationSettings
{
    public const string SectionName = "HouseholdInvitations";

    [Required]
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";

    [Range(1, 30)]
    public int ExpirationDays { get; init; } = 7;
}

public sealed class WebPushSettings
{
    public const string SectionName = "WebPush";
    public string Subject { get; init; } = "mailto:geral@fings.pt";
    public string PublicKey { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
}

public sealed class NotificationSettings
{
    public const string SectionName = "Notifications";
    public bool DefaultEnabled { get; init; } = true;
    public Dictionary<string, bool> DefaultEnabledByType { get; init; } = new(StringComparer.Ordinal);
    public int EventBatchSize { get; init; } = 50;
    public int DeliveryBatchSize { get; init; } = 100;
    public int PollIntervalSeconds { get; init; } = 10;
}
