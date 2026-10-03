using System.Security.Claims;

namespace otw.fings.api.management.Security;

public interface ICurrentUserContext
{
    long UserId { get; }
    string Email { get; }
}

public sealed class CurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public long UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? accessor.HttpContext?.User.FindFirstValue("sub");
            return long.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Token sem identificador de utilizador.");
        }
    }

    public string Email => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
        ?? accessor.HttpContext?.User.FindFirstValue("email")
        ?? string.Empty;
}
