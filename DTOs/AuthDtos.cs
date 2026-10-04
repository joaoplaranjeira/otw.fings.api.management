using System.ComponentModel.DataAnnotations;

namespace otw.fings.api.management.DTOs;

public sealed record SendOtpRequest([Required, EmailAddress] string Email);

public sealed record ValidateOtpRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(6, MinimumLength = 6)] string Code);

public sealed record OtpResponse(bool Success, string Message, DateTime? ExpiresAt = null, string? Token = null);

public sealed record RegisterUserRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(200)] string Username,
    [Required, EmailAddress, MaxLength(255)] string Email,
    [MaxLength(200)] string? HouseholdName = null,
    [MaxLength(50)] string? InvitationCode = null);

public sealed record UserResponse(long Id, string Name, string Username, string Email, bool IsActive);

public sealed record UpdateUserProfileRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(200)] string? Username);
