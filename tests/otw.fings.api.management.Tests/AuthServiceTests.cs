using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Services;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task OtpBypass_DoesNotSendEmailAndAcceptsFixedCode()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Id = 1,
            Name = "Owner",
            Username = "owner",
            Email = "owner@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var emailService = new TrackingEmailService();
        var service = new AuthService(
            new AuthRepository(db),
            emailService,
            new StubTokenService(),
            Options.Create(new OtpSettings { BypassEnabled = true }));

        var sendResult = await service.SendOtpAsync(user.Email, null, null, CancellationToken.None);
        var validateResult = await service.ValidateOtpAsync(user.Email, "123123", CancellationToken.None);

        Assert.True(sendResult.Success);
        Assert.Equal(0, emailService.SendCount);
        Assert.Empty(db.OtpCodes);
        Assert.True(validateResult.Success);
        Assert.Equal("bypass-token", validateResult.Token);
    }

    [Fact]
    public async Task OtpBypass_RejectsAnyOtherCode()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            Name = "Owner",
            Username = "owner",
            Email = "owner@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new AuthService(
            new AuthRepository(db),
            new TrackingEmailService(),
            new StubTokenService(),
            Options.Create(new OtpSettings { BypassEnabled = true }));

        var result = await service.ValidateOtpAsync("owner@example.com", "000000", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndNormalizesUsername()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            Name = "Owner",
            Username = "owner",
            Email = "owner@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UpdateProfileAsync(
            1,
            new("  Novo Nome  ", "  Novo.Username  "),
            CancellationToken.None);

        Assert.Equal("Novo Nome", result.Name);
        Assert.Equal("novo.username", result.Username);
        Assert.Equal("owner@example.com", result.Email);
    }

    [Fact]
    public async Task UpdateProfile_RejectsUsernameBelongingToAnotherUser()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        db.Users.AddRange(
            new User { Id = 1, Name = "Owner", Username = "owner", Email = "owner@example.com", InsertedDate = now, UpdatedDate = now },
            new User { Id = 2, Name = "Other", Username = "taken", Email = "other@example.com", InsertedDate = now, UpdatedDate = now });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateProfileAsync(
            1,
            new(null, "TAKEN"),
            CancellationToken.None));
    }

    private static AuthService CreateService(FingsDbContext db) => new(
        new AuthRepository(db),
        new TrackingEmailService(),
        new StubTokenService(),
        Options.Create(new OtpSettings()));

    private static FingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FingsDbContext(options);
    }

    private sealed class TrackingEmailService : IEmailService
    {
        public int SendCount { get; private set; }

        public Task<bool> SendOtpEmailAsync(string email, string otpCode, CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class StubTokenService : ITokenService
    {
        public string GenerateToken(User user, IEnumerable<string> permissions) => "bypass-token";
    }
}
