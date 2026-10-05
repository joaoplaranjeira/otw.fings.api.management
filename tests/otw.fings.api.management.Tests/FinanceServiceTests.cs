using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using otw.fings.api.management.Domain.Entities;
using otw.fings.api.management.Domain.Enums;
using otw.fings.api.management.DTOs;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories;
using otw.fings.api.management.Services;

namespace otw.fings.api.management.Tests;

public sealed class FinanceServiceTests
{
    [Fact]
    public async Task CreateHousehold_CreatesStandardCategoriesAndSubcategories()
    {
        await using var db = CreateDbContext();
        var service = new FinanceService(new FinanceRepository(db));

        var result = await service.CreateHouseholdAsync(
            1,
            new CreateHouseholdRequest("Família", "EUR", "Europe/Lisbon"),
            CancellationToken.None);

        var categories = await db.Categories
            .Include(x => x.Subcategories)
            .Where(x => x.HouseholdId == result.Id)
            .ToListAsync();
        Assert.Equal(13, categories.Count);
        Assert.Equal(87, categories.Sum(x => x.Subcategories.Count));
        Assert.All(categories, category => Assert.NotEmpty(category.Subcategories));
        Assert.All(categories, category =>
            Assert.Contains(category.Subcategories, x => x.Name == "Não aplicável"));

        var notApplicable = Assert.Single(categories, x => x.Name == "Não aplicável");
        Assert.Single(notApplicable.Subcategories, x => x.Name == "Não aplicável");

        var food = Assert.Single(categories, x => x.Name == "Alimentação");
        Assert.Equal("#22C55E", food.Color);
        Assert.Equal("shopping-cart", food.Icon);
        Assert.Contains(food.Subcategories, x => x.Name == "Supermercado");

        var housing = Assert.Single(categories, x => x.Name == "Habitação");
        Assert.Contains(housing.Subcategories, x => x.Name == "Limpeza");

        var membership = await db.HouseholdMembers.SingleAsync(x => x.HouseholdId == result.Id);
        Assert.Equal(1, membership.UserId);
        Assert.Equal(HouseholdRole.Owner, membership.Role);
    }

    [Fact]
    public async Task AddAndListHouseholdMember_AddsRegisteredActiveUser()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        db.Users.Add(CreateUser(2, "Maria", "maria", "maria@example.com"));
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));

        var added = await service.AddHouseholdMemberAsync(
            household.Id,
            1,
            new(" MARIA@example.com ", HouseholdRole.Member, "Maria", HouseholdRelationship.Wife, new DateOnly(1990, 4, 12)),
            CancellationToken.None);
        var members = await service.GetHouseholdMembersAsync(household.Id, 1, CancellationToken.None);

        Assert.Equal(2, added.UserId);
        Assert.Equal(HouseholdRole.Member, added.Role);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, x => x.UserId == 2 && x.Email == "maria@example.com");
    }

    [Fact]
    public async Task AddHouseholdMember_RejectsDuplicateAndUnknownUser()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ConflictException>(() => service.AddHouseholdMemberAsync(
            household.Id,
            1,
            new("owner@example.com", HouseholdRole.Member, "Owner", HouseholdRelationship.OtherRelative, new DateOnly(1980, 1, 1)),
            CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.AddHouseholdMemberAsync(
            household.Id,
            1,
            new("unknown@example.com", HouseholdRole.Member, "Desconhecido", HouseholdRelationship.OtherRelative, new DateOnly(1980, 1, 1)),
            CancellationToken.None));
    }

    [Fact]
    public async Task Administrator_CanManageMembersButNotPrivilegedRoles()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var administrator = CreateUser(2, "Admin", "admin", "admin@example.com");
        var target = CreateUser(3, "Membro", "member", "member@example.com");
        db.AddRange(
            administrator,
            target,
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = administrator.Id,
                Role = HouseholdRole.Administrator
            });
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));

        var added = await service.AddHouseholdMemberAsync(
            household.Id,
            administrator.Id,
            new(target.Email, HouseholdRole.Viewer, "Membro", HouseholdRelationship.OtherRelative, new DateOnly(1990, 1, 1)),
            CancellationToken.None);

        Assert.Equal(HouseholdRole.Viewer, added.Role);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddHouseholdMemberAsync(
            household.Id,
            administrator.Id,
            new("owner@example.com", HouseholdRole.Owner, "Owner", HouseholdRelationship.OtherRelative, new DateOnly(1980, 1, 1)),
            CancellationToken.None));
    }

    [Fact]
    public async Task Member_CannotManageHouseholdMembers()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var member = CreateUser(2, "Membro", "member", "member@example.com");
        var target = CreateUser(3, "Novo membro", "new-member", "new@example.com");
        db.AddRange(
            member,
            target,
            new HouseholdMember
            {
                HouseholdId = household.Id,
                UserId = member.Id,
                Role = HouseholdRole.Member
            });
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.AddHouseholdMemberAsync(
            household.Id,
            member.Id,
            new(target.Email, HouseholdRole.Member, "Novo membro", HouseholdRelationship.OtherRelative, new DateOnly(1990, 1, 1)),
            CancellationToken.None));
    }

    [Fact]
    public async Task RemoveHouseholdMember_RemovesMemberAndProtectsRequester()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var target = CreateUser(2, "Maria", "maria", "maria@example.com");
        var targetMembership = new HouseholdMember
        {
            HouseholdId = household.Id,
            UserId = target.Id,
            Role = HouseholdRole.Member
        };
        db.AddRange(target, targetMembership);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));

        await service.RemoveHouseholdMemberAsync(
            household.Id,
            targetMembership.Id,
            1,
            CancellationToken.None);

        Assert.False(await db.HouseholdMembers.AnyAsync(x => x.Id == targetMembership.Id));
        var ownerMembership = await db.HouseholdMembers.SingleAsync(x => x.UserId == 1);
        await Assert.ThrowsAsync<ValidationException>(() => service.RemoveHouseholdMemberAsync(
            household.Id,
            ownerMembership.Id,
            1,
            CancellationToken.None));
    }

    [Fact]
    public async Task HouseholdRoles_ReturnsAllSupportedRoles()
    {
        await using var db = CreateDbContext();
        var service = new FinanceService(new FinanceRepository(db));

        var roles = service.GetHouseholdRoles();

        Assert.Equal(Enum.GetValues<HouseholdRole>(), roles.Select(x => x.Value));
        Assert.Equal(["Owner", "Administrator", "Member", "Viewer"], roles.Select(x => x.Name));
    }

    [Fact]
    public async Task AddHouseholdMember_AddsElementWithoutUser()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        var added = await service.AddHouseholdMemberAsync(
            household.Id,
            1,
            new(null, null, "Tomás", HouseholdRelationship.Son, new DateOnly(2018, 7, 9)),
            CancellationToken.None);

        Assert.Null(added.UserId);
        Assert.Null(added.Role);
        Assert.Equal("Tomás", added.Name);
        Assert.Equal(HouseholdRelationship.Son, added.Relationship);
        Assert.Equal(new DateOnly(2018, 7, 9), added.BirthDate);
        Assert.Null(added.Email);
    }

    [Fact]
    public async Task UpdateHouseholdMember_UpdatesPersonalDetailsAndKeepsAccessRole()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var member = await db.HouseholdMembers.SingleAsync(x => x.UserId == 1);
        var service = new FinanceService(new FinanceRepository(db));

        var updated = await service.UpdateHouseholdMemberAsync(
            household.Id,
            member.Id,
            1,
            new("João", HouseholdRelationship.Self, new DateOnly(1988, 2, 3)),
            CancellationToken.None);

        Assert.Equal("João", updated.Name);
        Assert.Equal(HouseholdRelationship.Self, updated.Relationship);
        Assert.Equal(new DateOnly(1988, 2, 3), updated.BirthDate);
        Assert.Equal(HouseholdRole.Owner, updated.Role);
    }

    [Fact]
    public async Task AddHouseholdMember_RejectsMissingDetailsAndFutureBirthDate()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ValidationException>(() => service.AddHouseholdMemberAsync(
            household.Id,
            1,
            new(null, null, " ", HouseholdRelationship.Child, new DateOnly(2018, 1, 1)),
            CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.AddHouseholdMemberAsync(
            household.Id,
            1,
            new(null, null, "Futuro", HouseholdRelationship.Child, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateHouseholdMember_DoesNotDemoteOnlyOwner()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var member = await db.HouseholdMembers.SingleAsync(x => x.UserId == 1);
        var service = new FinanceService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateHouseholdMemberAsync(
            household.Id,
            member.Id,
            1,
            new("Owner", HouseholdRelationship.Self, new DateOnly(1980, 1, 1), HouseholdRole.Member),
            CancellationToken.None));
    }

    [Fact]
    public void HouseholdRelationships_ReturnsLocalizedCatalog()
    {
        var service = new FinanceService(null!);

        var relationships = service.GetHouseholdRelationships();

        Assert.Equal(Enum.GetValues<HouseholdRelationship>(), relationships.Select(x => x.Value));
        Assert.Contains(relationships, x => x.Value == HouseholdRelationship.Wife && x.Name == "Esposa");
        Assert.Contains(relationships, x => x.Value == HouseholdRelationship.OtherRelative && x.Name == "Outro familiar");
    }

    [Fact]
    public async Task CreateBudget_GeneratesOnePlannedIncomeForEveryMonth()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        var result = await service.CreateBudgetAsync(
            household.Id,
            1,
            new CreateBudgetPeriodRequest(
                "Primeiro semestre",
                new DateOnly(2027, 1, 20),
                new DateOnly(2027, 6, 30),
                1500m,
                [new BudgetAllocationRequest(category.Id, 600m)]),
            CancellationToken.None);

        var incomes = await db.Incomes.OrderBy(x => x.Date).ToListAsync();
        Assert.Equal(6, result.GeneratedIncomeCount);
        Assert.Equal(6, incomes.Count);
        Assert.All(incomes, income =>
        {
            Assert.Equal(1500m, income.Amount);
            Assert.Equal(IncomeOrigin.Budget, income.Origin);
            Assert.Equal(FinancialRecordStatus.Planned, income.Status);
            Assert.Equal(1, income.Date.Day);
        });
        Assert.Equal(new DateOnly(2027, 1, 1), incomes[0].Date);
        Assert.Equal(new DateOnly(2027, 6, 1), incomes[^1].Date);
    }

    [Fact]
    public async Task CreateBudget_RejectsOverlappingActivePeriod()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));
        await service.CreateBudgetAsync(
            household.Id, 1,
            new("Primeiro semestre", new(2027, 1, 1), new(2027, 6, 1), 1500m, null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateBudgetAsync(
            household.Id, 1,
            new("Sobreposto", new(2027, 6, 1), new(2027, 12, 1), 1600m, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateExpense_RejectsSubcategoryFromAnotherCategory()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var otherCategory = new Category { HouseholdId = household.Id, Name = "Transportes" };
        var otherSubcategory = new Subcategory { CategoryId = otherCategory.Id, Name = "Combustível" };
        db.AddRange(otherCategory, otherSubcategory);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateExpenseAsync(
            household.Id,
            1,
            new(category.Id, otherSubcategory.Id, new(2027, 1, 5), 20m, "Compra", "Loja", "500000000"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateExpense_WithoutExplicitLines_CreatesOneAssociatedLine()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        var result = await service.CreateExpenseAsync(
            household.Id,
            1,
            new(category.Id, null, new(2027, 1, 5), 20m, "Compra", "Loja", "500000000"),
            CancellationToken.None);

        var line = Assert.Single(result.Lines);
        Assert.Equal(result.Id, (await db.ExpenseLines.SingleAsync()).ExpenseId);
        Assert.Equal("Compra", line.Description);
        Assert.Equal(1m, line.Quantity);
        Assert.Equal(20m, line.UnitPrice);
        Assert.Equal(20m, line.Amount);
        Assert.Equal(category.Id, line.CategoryId);
    }

    [Fact]
    public async Task CreateReceiptExpense_CreatesOneMovementWithAllLines()
    {
        await using var db = CreateDbContext();
        var (household, food) = await SeedAsync(db);
        var health = new Category { HouseholdId = household.Id, Name = "Saúde" };
        db.Categories.Add(health);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));

        var result = await service.CreateExpenseAsync(
            household.Id,
            1,
            new CreateExpenseRequest(
                null,
                null,
                new(2027, 1, 5),
                25m,
                "Talão da loja",
                "Loja",
                "500000000",
                ExpenseOrigin.Receipt,
                [
                    new(food.Id, null, "Mercearia", 1, 10m, 10m),
                    new(health.Id, null, "Higiene", 1, 15m, 15m)
                ]),
            CancellationToken.None);

        Assert.Equal(25m, result.Amount);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(health.Id, result.CategoryId);
        Assert.Equal(1, await db.Expenses.CountAsync());
        Assert.Equal(2, await db.ExpenseLines.CountAsync());
    }

    [Fact]
    public async Task CreateExpense_RejectsLinesWhoseSumDiffersFromTotal()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateExpenseAsync(
            household.Id,
            1,
            new CreateExpenseRequest(
                category.Id,
                null,
                new(2027, 1, 5),
                20m,
                "Talão",
                "Loja",
                null,
                ExpenseOrigin.Receipt,
                [new(category.Id, null, "Produto", 1, 19m, 19m)]),
            CancellationToken.None));
    }

    [Fact]
    public async Task ExpenseLines_AcceptNegativeDiscountsButRejectZero()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        var created = await service.CreateExpenseAsync(
            household.Id,
            1,
            new CreateExpenseRequest(
                category.Id,
                null,
                new(2027, 1, 5),
                45m,
                "Talão com desconto",
                "Loja",
                null,
                ExpenseOrigin.Receipt,
                [
                    new(category.Id, null, "Produto", 1, 50m, 50m),
                    new(category.Id, null, "Desconto", 1, -5m, -5m)
                ]),
            CancellationToken.None);

        Assert.Equal(new[] { 50m, -5m }, created.Lines.Select(x => x.Amount).ToArray());

        var replaced = await service.ReplaceExpenseLinesAsync(
            household.Id,
            created.Id,
            1,
            new([
                new(category.Id, null, "Produto corrigido", 1, 55m, 55m),
                new(category.Id, null, "Desconto corrigido", 1, -10m, -10m)
            ]),
            CancellationToken.None);

        Assert.Equal(new[] { 55m, -10m }, replaced.Lines.Select(x => x.Amount).ToArray());

        await Assert.ThrowsAsync<ValidationException>(() => service.ReplaceExpenseLinesAsync(
            household.Id,
            created.Id,
            1,
            new([
                new(category.Id, null, "Produto", 1, 45m, 45m),
                new(category.Id, null, "Parcela inválida", null, null, 0m)
            ]),
            CancellationToken.None));
    }

    [Fact]
    public async Task Dashboard_CountsMovementTotalOnceAndSplitsLinesByCategory()
    {
        await using var db = CreateDbContext();
        var (household, food) = await SeedAsync(db);
        var health = new Category { HouseholdId = household.Id, Name = "Saúde" };
        db.Categories.Add(health);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));
        await service.CreateBudgetAsync(
            household.Id,
            1,
            new("Janeiro", new(2027, 1, 1), new(2027, 1, 1), 200m,
                [new(food.Id, 100m), new(health.Id, 100m)]),
            CancellationToken.None);
        await service.CreateExpenseAsync(
            household.Id,
            1,
            new CreateExpenseRequest(
                null, null, new(2027, 1, 5), 25m, "Talão", "Loja", null, ExpenseOrigin.Receipt,
                [
                    new(food.Id, null, "Mercearia", 1, 10m, 10m),
                    new(health.Id, null, "Higiene", 1, 15m, 15m)
                ]),
            CancellationToken.None);

        var dashboard = await service.GetDashboardAsync(household.Id, 1, new(2027, 1, 20), CancellationToken.None);

        Assert.Equal(25m, dashboard.ConfirmedExpenses);
        Assert.Equal(10m, Assert.Single(dashboard.Categories, x => x.CategoryId == food.Id).Spent);
        Assert.Equal(15m, Assert.Single(dashboard.Categories, x => x.CategoryId == health.Id).Spent);
    }

    [Fact]
    public async Task UpdateExpense_UpdatesAutomaticallyCreatedLine()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));
        var created = await service.CreateExpenseAsync(
            household.Id, 1,
            new(category.Id, null, new(2027, 1, 5), 20m, "Compra", "Loja", null),
            CancellationToken.None);

        var updated = await service.UpdateExpenseAsync(
            household.Id, created.Id, 1,
            new(category.Id, null, new(2027, 1, 6), 30m, "Compra corrigida", "Nova loja", null),
            CancellationToken.None);

        Assert.Equal(30m, updated.Amount);
        Assert.Equal(new DateOnly(2027, 1, 6), updated.Date);
        var line = Assert.Single(updated.Lines);
        Assert.Equal("Compra corrigida", line.Description);
        Assert.Equal(30m, line.Amount);
        Assert.Equal(30m, line.UnitPrice);
    }

    [Fact]
    public async Task UpdateExpense_WithMultipleLines_ReplacesLinesAndTotalAtomically()
    {
        await using var db = CreateDbContext();
        var (household, food) = await SeedAsync(db);
        var health = new Category { HouseholdId = household.Id, Name = "Saúde" };
        db.Categories.Add(health);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));
        var created = await service.CreateExpenseAsync(
            household.Id, 1,
            new CreateExpenseRequest(null, null, new(2027, 1, 5), 25m, "Talão", "Loja", null, ExpenseOrigin.Receipt,
                [new(food.Id, null, "Comida", 1, 10m, 10m), new(health.Id, null, "Higiene", 1, 15m, 15m)]),
            CancellationToken.None);

        var updated = await service.UpdateExpenseAsync(
            household.Id, created.Id, 1,
            new UpdateExpenseRequest(null, null, new(2027, 1, 5), 30m, "Talão corrigido", "Loja", null,
                [new(food.Id, null, "Comida", 1, 12m, 12m), new(health.Id, null, "Higiene", 1, 18m, 18m)]),
            CancellationToken.None);

        Assert.Equal(30m, updated.Amount);
        Assert.Equal(new[] { 12m, 18m }, updated.Lines.Select(x => x.Amount).ToArray());
        Assert.Equal(2, await db.ExpenseLines.CountAsync());
    }

    [Fact]
    public async Task ReplaceExpenseLines_RequiresSameMovementTotalAndUpdatesMainCategory()
    {
        await using var db = CreateDbContext();
        var (household, food) = await SeedAsync(db);
        var health = new Category { HouseholdId = household.Id, Name = "Saúde" };
        db.Categories.Add(health);
        await db.SaveChangesAsync();
        var service = new FinanceService(new FinanceRepository(db));
        var created = await service.CreateExpenseAsync(
            household.Id, 1,
            new(food.Id, null, new(2027, 1, 5), 25m, "Compra", "Loja", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() => service.ReplaceExpenseLinesAsync(
            household.Id, created.Id, 1,
            new([new(food.Id, null, "Inválida", 1, 24m, 24m)]),
            CancellationToken.None));

        var updated = await service.ReplaceExpenseLinesAsync(
            household.Id, created.Id, 1,
            new([new(food.Id, null, "Comida", 1, 5m, 5m), new(health.Id, null, "Higiene", 1, 20m, 20m)]),
            CancellationToken.None);

        Assert.Equal(health.Id, updated.CategoryId);
        Assert.Equal(new[] { 5m, 20m }, updated.Lines.Select(x => x.Amount).ToArray());
        Assert.Equal(2, await db.ExpenseLines.CountAsync());
    }

    [Fact]
    public async Task UpdateExpense_RejectsTotalChangeWhenCustomLinesAreNotSent()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));
        var created = await service.CreateExpenseAsync(
            household.Id, 1,
            new CreateExpenseRequest(category.Id, null, new(2027, 1, 5), 25m, "Talão", "Loja", null, ExpenseOrigin.Receipt,
                [new(category.Id, null, "Produto A", 1, 10m, 10m), new(category.Id, null, "Produto B", 1, 15m, 15m)]),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateExpenseAsync(
            household.Id, created.Id, 1,
            new(category.Id, null, new(2027, 1, 5), 30m, "Talão", "Loja", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCategoryAndSubcategory_ChangesTheirConfiguration()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));
        var subcategory = await service.CreateSubcategoryAsync(
            household.Id, category.Id, 1, new("Supermercado"), CancellationToken.None);

        var updatedCategory = await service.UpdateCategoryAsync(
            household.Id, category.Id, 1,
            new("Alimentação e bebidas", "#123456", "shopping-cart"),
            CancellationToken.None);
        var updatedSubcategory = await service.UpdateSubcategoryAsync(
            household.Id, category.Id, subcategory.Id, 1,
            new("Compras de supermercado"),
            CancellationToken.None);

        Assert.Equal("Alimentação e bebidas", updatedCategory.Name);
        Assert.Equal("#123456", updatedCategory.Color);
        Assert.Equal("shopping-cart", updatedCategory.Icon);
        Assert.Equal("Compras de supermercado", updatedSubcategory.Name);
    }

    [Fact]
    public async Task CreateCategory_AddsNotApplicableSubcategory()
    {
        await using var db = CreateDbContext();
        var (household, _) = await SeedAsync(db);
        var service = new FinanceService(new FinanceRepository(db));

        var category = await service.CreateCategoryAsync(
            household.Id, 1, new("Categoria personalizada", null, null), CancellationToken.None);

        var subcategory = Assert.Single(category.Subcategories);
        Assert.Equal("Não aplicável", subcategory.Name);
    }

    [Fact]
    public async Task UpdateRecurringExpense_UpdatesRuleAndPreservesMaterializationProgress()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var repository = new FinanceRepository(db);
        var service = new FinanceService(repository);
        var created = await service.CreateRecurringExpenseAsync(
            household.Id,
            1,
            new(category.Id, null, "Internet", "Operador", null, 35m,
                RecurrenceFrequency.Monthly, new DateOnly(2027, 1, 10), null),
            CancellationToken.None);
        await new RecurringExpenseMaterializer(repository)
            .MaterializeDueAsync(new DateOnly(2027, 2, 10), CancellationToken.None);

        var updated = await service.UpdateRecurringExpenseAsync(
            household.Id,
            created.Id,
            1,
            new(category.Id, null, "Internet fibra", "Novo operador", null, 42m,
                RecurrenceFrequency.Monthly, new DateOnly(2027, 1, 10), new DateOnly(2027, 12, 10)),
            CancellationToken.None);

        Assert.Equal("Internet fibra", updated.Description);
        Assert.Equal(42m, updated.Amount);
        Assert.Equal(new DateOnly(2027, 3, 10), updated.NextOccurrenceDate);
        Assert.True(updated.IsActive);
        Assert.Equal(2, await db.Expenses.CountAsync());
    }

    [Fact]
    public async Task RecurringExpenseMaterializer_CreatesDueOccurrencesOnlyOnce()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var repository = new FinanceRepository(db);
        var service = new FinanceService(repository);
        await service.CreateRecurringExpenseAsync(
            household.Id,
            1,
            new(category.Id, null, "Internet", "Operador", "500000000", 35m,
                RecurrenceFrequency.Monthly, new DateOnly(2027, 1, 10), new DateOnly(2027, 3, 10)),
            CancellationToken.None);
        var materializer = new RecurringExpenseMaterializer(repository);

        var firstRun = await materializer.MaterializeDueAsync(new DateOnly(2027, 4, 1), CancellationToken.None);
        var secondRun = await materializer.MaterializeDueAsync(new DateOnly(2027, 4, 1), CancellationToken.None);

        Assert.Equal(3, firstRun);
        Assert.Equal(0, secondRun);
        Assert.Equal(3, await db.Expenses.CountAsync());
        var expenses = await db.Expenses.Include(x => x.Lines).ToListAsync();
        Assert.All(expenses, x =>
        {
            Assert.Equal(ExpenseOrigin.Recurring, x.Origin);
            var line = Assert.Single(x.Lines);
            Assert.Equal(x.Amount, line.Amount);
        });
    }

    [Fact]
    public async Task CreateRecurringExpense_WithMaterializeNow_CreatesEveryDueOccurrence()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var repository = new FinanceRepository(db);
        var materializer = new RecurringExpenseMaterializer(repository);
        var service = new FinanceService(repository, materializer);
        var today = TodayInLisbon();
        var startDate = today.AddMonths(-2);

        var created = await service.CreateRecurringExpenseAsync(
            household.Id,
            1,
            new(category.Id, null, "Internet", "Operador", null, 35m,
                RecurrenceFrequency.Monthly, startDate, null, true),
            CancellationToken.None);

        var expenses = await db.Expenses.OrderBy(x => x.Date).ToListAsync();
        Assert.Equal(3, expenses.Count);
        Assert.Equal([startDate, startDate.AddMonths(1), startDate.AddMonths(2)], expenses.Select(x => x.Date));
        Assert.Equal(startDate.AddMonths(3), created.NextOccurrenceDate);
    }

    [Fact]
    public async Task MaterializeRecurringExpense_CreatesPendingOccurrencesIdempotently()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var repository = new FinanceRepository(db);
        var materializer = new RecurringExpenseMaterializer(repository);
        var service = new FinanceService(repository, materializer);
        var today = TodayInLisbon();
        var recurring = await service.CreateRecurringExpenseAsync(
            household.Id,
            1,
            new(category.Id, null, "Seguro", null, null, 20m,
                RecurrenceFrequency.Monthly, today.AddMonths(-1), null),
            CancellationToken.None);

        var first = await service.MaterializeRecurringExpenseAsync(
            household.Id, recurring.Id, 1, CancellationToken.None);
        var second = await service.MaterializeRecurringExpenseAsync(
            household.Id, recurring.Id, 1, CancellationToken.None);

        Assert.Equal(2, first.CreatedCount);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(today.AddMonths(1), second.NextOccurrenceDate);
        Assert.Equal(2, await db.Expenses.CountAsync());
    }

    [Fact]
    public async Task GetFrequentExpenseSuggestions_ReturnsTopTenDistinctCombinationsFromLastThirtyDays()
    {
        await using var db = CreateDbContext();
        var (household, category) = await SeedAsync(db);
        var subcategory = new Subcategory { CategoryId = category.Id, Name = "Supermercado" };
        db.Subcategories.Add(subcategory);
        await db.SaveChangesAsync();

        var today = new DateOnly(2027, 1, 30);
        Expense CreateExpense(
            string? merchantName,
            DateOnly date,
            FinancialRecordStatus status = FinancialRecordStatus.Confirmed) => new()
        {
            HouseholdId = household.Id,
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            Date = date,
            Amount = 10m,
            Description = "Compra",
            MerchantName = merchantName,
            MerchantTaxNumber = "500000000",
            Status = status
        };

        db.Expenses.AddRange(
            CreateExpense("Mercado Alfa", today.AddDays(-1)),
            CreateExpense("Mercado Alfa", today.AddDays(-2)),
            CreateExpense("Mercado Alfa", today.AddDays(-3)),
            CreateExpense("Mercado Beta", today.AddDays(-1)),
            CreateExpense("Mercado Beta", today.AddDays(-2)),
            CreateExpense("Mercado Beta", today, FinancialRecordStatus.Cancelled),
            CreateExpense("Antigo", today.AddDays(-30)),
            CreateExpense("Futuro", today.AddDays(1)),
            CreateExpense(null, today));
        db.Expenses.AddRange(Enumerable.Range(0, 9)
            .Select(index => CreateExpense($"Loja {index:00}", today.AddDays(-5))));
        await db.SaveChangesAsync();

        var repository = new FinanceRepository(db);
        var clock = new FixedTimeProvider(new DateTimeOffset(2027, 1, 30, 12, 0, 0, TimeSpan.Zero));
        var service = new FinanceService(repository, timeProvider: clock);

        var result = await service.GetFrequentExpenseSuggestionsAsync(
            household.Id,
            1,
            CancellationToken.None);

        Assert.Equal(10, result.Count);
        Assert.Equal("Mercado Alfa", result[0].MerchantName);
        Assert.Equal("Compra", result[0].Description);
        Assert.Equal(3, result[0].OccurrenceCount);
        Assert.Equal(today.AddDays(-1), result[0].LastOccurrenceDate);
        Assert.Equal(category.Id, result[0].CategoryId);
        Assert.Equal(subcategory.Id, result[0].SubcategoryId);
        Assert.Equal("Mercado Beta", result[1].MerchantName);
        Assert.Equal(2, result[1].OccurrenceCount);
        Assert.DoesNotContain(result, x => x.MerchantName is "Antigo" or "Futuro" or "Loja 08");
    }

    private static DateOnly TodayInLisbon()
    {
        var lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, lisbon).DateTime);
    }

    private static FingsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FingsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new FingsDbContext(options);
    }

    private static async Task<(Household Household, Category Category)> SeedAsync(FingsDbContext db)
    {
        var user = new User
        {
            Id = 1,
            Name = "Owner",
            Username = "owner",
            Email = "owner@example.com",
            InsertedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        var household = new Household { Name = "Família" };
        var membership = new HouseholdMember { HouseholdId = household.Id, UserId = user.Id, Role = HouseholdRole.Owner };
        var category = new Category { HouseholdId = household.Id, Name = "Alimentação" };
        db.AddRange(user, household, membership, category);
        await db.SaveChangesAsync();
        return (household, category);
    }

    private static User CreateUser(long id, string name, string username, string email) => new()
    {
        Id = id,
        Name = name,
        Username = username,
        Email = email,
        InsertedDate = DateTime.UtcNow,
        UpdatedDate = DateTime.UtcNow
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
