using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Domain.Enums;
using otw.fings.api.management.DTOs;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Services;

namespace otw.fings.api.management.Tests;

public sealed class ReceiptParseReviewServiceTests
{
    [Fact]
    public async Task ValidateAndGetHistory_StoresQualityFeedback()
    {
        await using var db = CreateDbContext();
        var (household, record) = await SeedAsync(db, HouseholdRole.Owner);
        var service = new ReceiptParseReviewService(new FinanceRepository(db));

        var validated = await service.ValidateAsync(
            household.Id,
            record.Id,
            1,
            new ValidateReceiptParseRequest(false, "Categorias incorretas"),
            CancellationToken.None);
        var history = await service.GetHistoryAsync(household.Id, 1, 50, CancellationToken.None);

        Assert.Equal(ReceiptParseValidationStatus.Invalid, validated.ValidationStatus);
        Assert.Equal("Categorias incorretas", validated.ValidationNotes);
        Assert.NotNull(validated.ValidatedAtUtc);
        Assert.Equal(1, history.Total);
        Assert.Equal(0, history.Valid);
        Assert.Equal(1, history.Invalid);
        Assert.Equal(0m, history.ValidRate);
    }

    [Fact]
    public async Task Validate_RejectsViewer()
    {
        await using var db = CreateDbContext();
        var (household, record) = await SeedAsync(db, HouseholdRole.Viewer);
        var service = new ReceiptParseReviewService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ValidateAsync(
            household.Id,
            record.Id,
            1,
            new ValidateReceiptParseRequest(true, null),
            CancellationToken.None));
    }

    private static FingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FingsDbContext(options);
    }

    private static async Task<(Household Household, ReceiptParseRecord Record)> SeedAsync(
        FingsDbContext db,
        HouseholdRole role)
    {
        var user = new User
        {
            Id = 1,
            Name = "User",
            Username = "user",
            Email = "user@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        var household = new Household { Name = "Família" };
        var membership = new HouseholdMember { HouseholdId = household.Id, UserId = user.Id, Role = role };
        var record = new ReceiptParseRecord
        {
            HouseholdId = household.Id,
            RequestedByUserId = user.Id,
            Model = "test-model",
            LineCount = 3,
            CategorizedLineCount = 2,
            AverageConfidence = 0.75m
        };
        db.AddRange(user, household, membership, record);
        await db.SaveChangesAsync();
        return (household, record);
    }
}
