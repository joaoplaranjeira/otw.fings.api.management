namespace otw.fings.api.management.Domain.Entities;

public sealed class User
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Profile { get; set; }
    public bool IsActive { get; set; } = true;
    public string InsertedUser { get; set; } = "System";
    public string? UpdatedUser { get; set; }
    public DateTime InsertedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public sealed class UserPermission
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public User User { get; set; } = null!;
}

public sealed class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? BlockedUntil { get; set; }
}
