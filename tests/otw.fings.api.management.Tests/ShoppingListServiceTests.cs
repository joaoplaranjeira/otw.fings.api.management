using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Domain.Enums;
using otw.fings.api.management.DTOs;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Services;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Tests;

public sealed class ShoppingListServiceTests
{
    [Fact]
    public async Task SupportsMultipleActiveListsAndManualItems()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);

        var supermarket = await service.CreateListAsync(
            household.Id, 1, new("Supermercado"), CancellationToken.None);
        var party = await service.CreateListAsync(
            household.Id, 1, new("Festa"), CancellationToken.None);
        await service.AddItemAsync(
            household.Id,
            supermarket.Id,
            1,
            new(ProductTypeId(db, "Leite"), Brand: "Mimosa", Quantity: 2, Unit: "un", ExpectedPrice: 1.29m),
            CancellationToken.None);
        await service.AddItemAsync(
            household.Id,
            party.Id,
            1,
            new(ProductTypeId(db, "Gelo"), Quantity: 3, Unit: "sacos", Notes: "Comprar no fim"),
            CancellationToken.None);

        var lists = await service.GetListsAsync(
            household.Id, 1, true, false, CancellationToken.None);

        Assert.Equal(2, lists.Count);
        Assert.All(lists, list => Assert.Equal(ShoppingListStatus.Active, list.Status));
        Assert.Contains(lists, list => list.Name == "Supermercado" && list.ActiveItemCount == 1);
        Assert.Contains(lists, list => list.Name == "Festa" && list.ActiveItemCount == 1);
        var milk = Assert.Single(lists.Single(list => list.Name == "Supermercado").Items);
        Assert.Equal("Mimosa", milk.Brand);
        Assert.Equal(2, milk.Quantity);
        Assert.Equal(1.29m, milk.ExpectedPrice);
    }

    [Fact]
    public async Task ProductPhotoRecognitionAddsDetectedProduct()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var recognizer = new StubProductImageRecognizer(new(
            "Leite", "Mimosa", "Meio gordo", 1, "L", 0.96m));
        var service = CreateService(db, recognizer);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);

        await using var image = new MemoryStream([1, 2, 3]);
        var result = await service.AddItemFromImageAsync(
            household.Id, list.Id, 1, image, "image/jpeg", image.Length, CancellationToken.None);

        Assert.Equal("Leite", result.Item.ProductTypeName);
        Assert.Equal(ProductTypeId(db, "Leite"), result.Item.ProductTypeId);
        Assert.Equal("Mimosa", result.Item.Brand);
        Assert.Equal(1, result.Item.Quantity);
        Assert.Null(result.Item.ExpectedPrice);
        Assert.Equal(ShoppingListItemSource.ProductPhoto, result.Item.Source);
        Assert.Equal(0.96m, result.Item.DetectionConfidence);
        Assert.Equal("LEITE", await db.ShoppingListItems.Select(x => x.NormalizedProductTypeName).SingleAsync());
    }

    [Fact]
    public async Task ProductPhotoDoesNotRequireExistingOrPreselectedProductType()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var recognizer = new StubProductImageRecognizer(new(
            "Bolachas", "Triunfo", null, 1, "embalagem", 0.91m));
        var service = CreateService(db, recognizer);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);

        await using var image = new MemoryStream([1, 2, 3]);
        var result = await service.AddItemFromImageAsync(
            household.Id, list.Id, 1, image, "image/jpeg", image.Length, CancellationToken.None);

        Assert.Null(result.Item.ProductTypeId);
        Assert.Equal("Bolachas", result.Item.ProductTypeName);
        Assert.Equal("Triunfo", result.Item.Brand);
    }

    [Fact]
    public async Task BrandQuantityAndExpectedPriceAreOptional()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);

        var item = await service.AddItemAsync(
            household.Id, list.Id, 1, new(ProductTypeId(db, "Sal")), CancellationToken.None);

        Assert.Null(item.Brand);
        Assert.Null(item.Quantity);
        Assert.Null(item.ExpectedPrice);
    }

    [Fact]
    public async Task ProductTypeSelectionCreatesItemAndOptionalDetailsStayOnItem()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);

        var item = await service.AddItemAsync(
            household.Id,
            list.Id,
            1,
            new(ProductTypeId(db, "Leite"), Brand: "Mimosa", Quantity: 2, Unit: "un", ExpectedPrice: 1.49m),
            CancellationToken.None);

        Assert.Equal(ProductTypeId(db, "Leite"), item.ProductTypeId);
        Assert.Equal("Leite", item.ProductTypeName);
        Assert.Equal("Mimosa", item.Brand);
        Assert.Equal(2, item.Quantity);
        Assert.Equal("un", item.Unit);
        Assert.Equal(1.49m, item.ExpectedPrice);
    }

    [Fact]
    public async Task NewExpenseCreatesPendingMatchesAndApprovalOnlyCompletesSelectedItem()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);
        var weekly = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);
        var reserve = await service.CreateListAsync(household.Id, 1, new("Reserva"), CancellationToken.None);
        var weeklyItem = await service.AddItemAsync(
            household.Id, weekly.Id, 1, new(ProductTypeId(db, "Leite")), CancellationToken.None);
        var reserveItem = await service.AddItemAsync(
            household.Id, reserve.Id, 1, new(ProductTypeId(db, "Leite")), CancellationToken.None);
        var category = await db.Categories.SingleAsync();
        var finance = new FinanceService(
            new FinanceRepository(db),
            shoppingListExpenseMatcher: service);

        await finance.CreateExpenseAsync(
            household.Id,
            1,
            new CreateExpenseRequest(
                category.Id,
                null,
                new DateOnly(2026, 11, 11),
                1.29m,
                "Compras",
                "Supermercado",
                null,
                ExpenseOrigin.Receipt,
                [new(category.Id, null, "Leite Mimosa meio gordo", 1, 1.29m, 1.29m)]),
            CancellationToken.None);

        var pending = await service.GetExpenseMatchesAsync(
            household.Id, 1, ShoppingListMatchStatus.Pending, CancellationToken.None);
        Assert.Equal(2, pending.Count);

        var approvedMatch = Assert.Single(pending, x => x.ShoppingListItemId == weeklyItem.Id);
        await service.ReviewExpenseMatchAsync(
            household.Id,
            approvedMatch.Id,
            1,
            new(ShoppingListMatchStatus.Approved),
            CancellationToken.None);

        Assert.Equal(
            ShoppingListItemStatus.Purchased,
            (await db.ShoppingListItems.SingleAsync(x => x.Id == weeklyItem.Id)).Status);
        Assert.Equal(
            ShoppingListItemStatus.Active,
            (await db.ShoppingListItems.SingleAsync(x => x.Id == reserveItem.Id)).Status);
        Assert.Single(await service.GetExpenseMatchesAsync(
            household.Id, 1, ShoppingListMatchStatus.Pending, CancellationToken.None));
    }

    [Fact]
    public async Task RejectingExpenseMatchKeepsItemActive()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);
        var item = await service.AddItemAsync(
            household.Id, list.Id, 1, new(ProductTypeId(db, "Arroz")), CancellationToken.None);
        var expense = await CreateExpenseAsync(db, household.Id, "Arroz agulha");
        await service.DetectMatchesAsync(expense, CancellationToken.None);
        var match = Assert.Single(await service.GetExpenseMatchesAsync(
            household.Id, 1, ShoppingListMatchStatus.Pending, CancellationToken.None));

        await service.ReviewExpenseMatchAsync(
            household.Id, match.Id, 1, new(ShoppingListMatchStatus.Rejected), CancellationToken.None);

        Assert.Equal(
            ShoppingListItemStatus.Active,
            (await db.ShoppingListItems.SingleAsync(x => x.Id == item.Id)).Status);
    }

    [Fact]
    public async Task PartialPurchaseReducesQuantityAndKeepsItemActive()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);
        var item = await service.AddItemAsync(
            household.Id,
            list.Id,
            1,
            new(ProductTypeId(db, "Leite"), Quantity: 3, Unit: "un"),
            CancellationToken.None);
        var expense = await CreateExpenseAsync(db, household.Id, "Leite Mimosa", quantity: 1);
        await service.DetectMatchesAsync(expense, CancellationToken.None);
        var match = Assert.Single(await service.GetExpenseMatchesAsync(
            household.Id, 1, ShoppingListMatchStatus.Pending, CancellationToken.None));

        Assert.Equal(3, match.RequestedQuantityAtDetection);
        Assert.Equal(1, match.DetectedPurchasedQuantity);
        var reviewed = await service.ReviewExpenseMatchAsync(
            household.Id, match.Id, 1, new(ShoppingListMatchStatus.Approved), CancellationToken.None);

        var storedItem = await db.ShoppingListItems.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(ShoppingListItemStatus.Active, storedItem.Status);
        Assert.Equal(2, storedItem.Quantity);
        Assert.Equal(1, reviewed.ApprovedPurchasedQuantity);
        Assert.Equal(2, reviewed.RemainingQuantity);
    }

    [Fact]
    public async Task ApprovalRequiresPurchasedQuantityWhenExpenseDoesNotProvideIt()
    {
        await using var db = CreateDbContext();
        var household = await SeedAsync(db);
        var service = CreateService(db);
        var list = await service.CreateListAsync(household.Id, 1, new("Semanal"), CancellationToken.None);
        await service.AddItemAsync(
            household.Id, list.Id, 1, new(ProductTypeId(db, "Arroz"), Quantity: 2), CancellationToken.None);
        var expense = await CreateExpenseAsync(db, household.Id, "Arroz agulha");
        await service.DetectMatchesAsync(expense, CancellationToken.None);
        var match = Assert.Single(await service.GetExpenseMatchesAsync(
            household.Id, 1, ShoppingListMatchStatus.Pending, CancellationToken.None));

        await Assert.ThrowsAsync<ValidationException>(() => service.ReviewExpenseMatchAsync(
            household.Id, match.Id, 1, new(ShoppingListMatchStatus.Approved), CancellationToken.None));

        var reviewed = await service.ReviewExpenseMatchAsync(
            household.Id,
            match.Id,
            1,
            new(ShoppingListMatchStatus.Approved, PurchasedQuantity: 1),
            CancellationToken.None);
        Assert.Equal(1, reviewed.RemainingQuantity);
    }

    [Theory]
    [InlineData("Leite", "LEITE MIMOSA 1L", 0.82)]
    [InlineData("Café em grão", "Cafe em grao", 1)]
    [InlineData("Arroz agulha", "Massa esparguete", 0)]
    public void Similarity_NormalizesAndMatchesConservatively(string product, string expenseLine, decimal expected)
    {
        var result = ShoppingListService.Similarity(
            ShoppingListService.Normalize(product),
            ShoppingListService.Normalize(expenseLine));

        Assert.Equal(expected, result);
    }

    private static ShoppingListService CreateService(
        FingsDbContext db,
        IProductImageRecognizer? recognizer = null) => new(
        new ShoppingListRepository(db),
        new FinanceRepository(db),
        recognizer ?? new StubProductImageRecognizer(new("Produto", null, null, null, null, 0.8m)));

    private static async Task<Expense> CreateExpenseAsync(
        FingsDbContext db,
        Guid householdId,
        string description,
        decimal? quantity = null)
    {
        var category = await db.Categories.SingleAsync();
        var expense = new Expense
        {
            HouseholdId = householdId,
            CreatedByUserId = 1,
            CategoryId = category.Id,
            Category = category,
            Date = new DateOnly(2026, 11, 11),
            Amount = 2,
            Description = "Compras"
        };
        expense.Lines.Add(new ExpenseLine
        {
            ExpenseId = expense.Id,
            CategoryId = category.Id,
            Category = category,
            Description = description,
            Quantity = quantity,
            Amount = 2,
            Position = 1
        });
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return expense;
    }

    private static async Task<Household> SeedAsync(FingsDbContext db)
    {
        var user = new User
        {
            Id = 1,
            Name = "Owner",
            Username = "owner",
            Email = "owner@example.com",
            Role = "User",
            Profile = "Default",
            IsActive = true
        };
        var household = new Household { Name = "Casa" };
        var category = new Category { HouseholdId = household.Id, Name = "Alimentação" };
        db.AddRange(
            user,
            household,
            new HouseholdMember { HouseholdId = household.Id, UserId = user.Id, Role = HouseholdRole.Owner },
            category,
            new ProductType { HouseholdId = household.Id, CategoryId = category.Id, Name = "Leite", NormalizedName = "LEITE" },
            new ProductType { HouseholdId = household.Id, CategoryId = category.Id, Name = "Gelo", NormalizedName = "GELO" },
            new ProductType { HouseholdId = household.Id, CategoryId = category.Id, Name = "Sal", NormalizedName = "SAL" },
            new ProductType { HouseholdId = household.Id, CategoryId = category.Id, Name = "Arroz", NormalizedName = "ARROZ" });
        await db.SaveChangesAsync();
        return household;
    }

    private static Guid ProductTypeId(FingsDbContext db, string name) =>
        db.ProductTypes.Single(x => x.Name == name).Id;

    private static FingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FingsDbContext(options);
    }

    private sealed class StubProductImageRecognizer(ProductRecognitionResponse response) : IProductImageRecognizer
    {
        public Task<ProductRecognitionResponse> RecognizeAsync(
            Stream image,
            string contentType,
            long length,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
