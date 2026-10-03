using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Services;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Tests;

public sealed class TokenServiceTests
{
    [Fact]
    public void GenerateToken_ExpiresOneCalendarMonthAfterIssue()
    {
        var service = new TokenService(Options.Create(new JwtSettings
        {
            Issuer = "issuer",
            Audience = "audience",
            SecretKey = "a-development-secret-with-more-than-32-characters"
        }));
        var token = service.GenerateToken(new User
        {
            Id = 7,
            Name = "User",
            Username = "user",
            Email = "user@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        }, []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Iat).Value)).UtcDateTime;
        Assert.InRange(jwt.ValidTo, issuedAt.AddMonths(1).AddSeconds(-1), issuedAt.AddMonths(1).AddSeconds(1));
    }
}
