using System.ComponentModel.DataAnnotations;

namespace otw.fings.api.management.DTOs;

public sealed record CreateHouseholdInvitationRequest(
    [Required, EmailAddress, MaxLength(255)] string Email,
    HouseholdRole Role);

public sealed record HouseholdInvitationCreatedResponse(
    Guid Id,
    Guid HouseholdId,
    string HouseholdName,
    string Email,
    HouseholdRole Role,
    string Code,
    string InvitationUrl,
    bool EmailSent,
    HouseholdInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt);

public sealed record HouseholdInvitationResponse(
    Guid Id,
    Guid HouseholdId,
    string HouseholdName,
    string Email,
    HouseholdRole Role,
    bool EmailSent,
    HouseholdInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt);

public sealed record HouseholdInvitationPreviewResponse(
    string HouseholdName,
    string MaskedEmail,
    HouseholdRole Role,
    DateTimeOffset ExpiresAt);

public sealed record HouseholdInvitationEmailResponse(
    Guid Id,
    string Email,
    bool EmailSent);
