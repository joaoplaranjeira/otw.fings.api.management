using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Domain.Enums;
using otw.fings.api.management.DTOs;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Services;
using otw.fings.api.management.Services.Interfaces;
using otw.fings.api.management.Settings;

namespace otw.fings.api.management.Tests;

public sealed class HouseholdInvitationServiceTests
{
    [Fact]
    public async Task Create_GeneratesCodeWithoutSendingEmail()
    {
        await using var db = CreateDbContext();
        var (household, owner) = await SeedOwnerAsync(db);
        var emailService = new InvitationEmailService(false);
        var service = CreateService(db, emailService);

        var result = await service.CreateAsync(
            household.Id,
            owner.Id,
            new CreateHouseholdInvitationRequest(" MARIA@example.com ", HouseholdRole.Member),
            CancellationToken.None);

        var stored = await db.HouseholdInvitations.SingleAsync();
        Assert.StartsWith("FINGS-", result.Code);
        Assert.Equal($"http://localhost:5173/register?invitationCode={result.Code}", result.InvitationUrl);
        Assert.Equal("maria@example.com", result.Email);
        Assert.False(result.EmailSent);
        Assert.Equal(HouseholdInvitationStatus.Pending, stored.Status);
        Assert.NotEqual(result.Code, stored.CodeHash);
        Assert.Equal(HouseholdInvitationCodes.Hash(result.Code), stored.CodeHash);
        Assert.Equal(0, emailService.InvitationSendCount);
        Assert.Equal(result.Code, stored.Code);

        var emailResult = await service.SendEmailAsync(
            household.Id,
            stored.Id,
            owner.Id,
            CancellationToken.None);

        Assert.False(emailResult.EmailSent);
        Assert.Equal(1, emailService.InvitationSendCount);

        var regenerated = await service.RegenerateCodeAsync(
            household.Id,
            stored.Id,
            owner.Id,
            CancellationToken.None);

        Assert.NotEqual(result.Code, regenerated.Code);
        Assert.False(regenerated.EmailSent);
        Assert.Equal(1, emailService.InvitationSendCount);
    }

    [Fact]
    public async Task Accept_AddsExistingUserWithRoleAndConsumesInvitation()
    {
        await using var db = CreateDbContext();
        var (household, owner) = await SeedOwnerAsync(db);
        var invitedUser = CreateUser(2, "Maria", "maria", "maria@example.com");
        const string code = "FINGS-ABCD-2345";
        var invitation = new HouseholdInvitation
        {
            HouseholdId = household.Id,
            Email = invitedUser.Email,
            Role = HouseholdRole.Viewer,
            CodeHash = HouseholdInvitationCodes.Hash(code),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            CreatedByUserId = owner.Id
        };
        db.AddRange(invitedUser, invitation);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.AcceptAsync(code.ToLowerInvariant(), invitedUser.Id, CancellationToken.None);

        Assert.Equal(HouseholdRole.Viewer, result.Role);
        Assert.True(await db.HouseholdMembers.AnyAsync(
            x => x.HouseholdId == household.Id && x.UserId == invitedUser.Id && x.Role == HouseholdRole.Viewer));
        Assert.Equal(HouseholdInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(invitedUser.Id, invitation.AcceptedByUserId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(code, invitedUser.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Administrator_CannotCreatePrivilegedInvitation()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedOwnerAsync(db);
        var administrator = CreateUser(2, "Admin", "admin", "admin@example.com");
        db.AddRange(
            administrator,
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = administrator.Id,
                Role = HouseholdRole.Administrator
            });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(
            household.Id,
            administrator.Id,
            new CreateHouseholdInvitationRequest("new@example.com", HouseholdRole.Owner),
            CancellationToken.None));
    }

    [Fact]
    public async Task Preview_RejectsExpiredInvitation()
    {
        await using var db = CreateDbContext();
        var (household, owner) = await SeedOwnerAsync(db);
        const string code = "FINGS-ABCD-2345";
        db.HouseholdInvitations.Add(new HouseholdInvitation
        {
            HouseholdId = household.Id,
            Email = "maria@example.com",
            Role = HouseholdRole.Member,
            CodeHash = HouseholdInvitationCodes.Hash(code),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedByUserId = owner.Id
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<GoneException>(() =>
            service.GetPreviewAsync(code, CancellationToken.None));
    }

    private static HouseholdInvitationService CreateService(
        FingsDbContext db,
        InvitationEmailService? emailService = null) =>
        new(
            new HouseholdInvitationRepository(db),
            new FinanceRepository(db),
            new AuthRepository(db),
            emailService ?? new InvitationEmailService(true),
            Options.Create(new HouseholdInvitationSettings
            {
                FrontendBaseUrl = "http://localhost:5173",
                ExpirationDays = 7
            }));

    private static async Task<(Household Household, User Owner)> SeedOwnerAsync(FingsDbContext db)
    {
        var owner = CreateUser(1, "Owner", "owner", "owner@example.com");
        var household = new Household { Name = "Família" };
        db.AddRange(
            owner,
            household,
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = owner.Id,
                Role = HouseholdRole.Owner
            });
        await db.SaveChangesAsync();
        return (household, owner);
    }

    private static User CreateUser(long id, string name, string username, string email) => new()
    {
        Id = id,
        Name = name,
        Username = username,
        Email = email,
        IsActive = true,
        InsertedDate = DateTime.UtcNow,
        UpdatedDate = DateTime.UtcNow
    };

    private static FingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FingsDbContext(options);
    }

    private sealed class InvitationEmailService(bool invitationResult) : IEmailService
    {
        public int InvitationSendCount { get; private set; }

        public Task<bool> SendOtpEmailAsync(
            string email,
            string otpCode,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> SendHouseholdInvitationEmailAsync(
            string email,
            string householdName,
            string role,
            string invitationCode,
            string invitationUrl,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            InvitationSendCount++;
            return Task.FromResult(invitationResult);
        }
    }
}
