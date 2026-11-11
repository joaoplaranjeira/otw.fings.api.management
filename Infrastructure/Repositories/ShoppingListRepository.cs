using Microsoft.EntityFrameworkCore;
using otw.fings.api.management.Infrastructure.Data;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;

namespace otw.fings.api.management.Infrastructure.Repositories;

public sealed class ShoppingListRepository(FingsDbContext dbContext) : IShoppingListRepository
{
    public async Task<IReadOnlyList<ShoppingList>> GetListsAsync(
        Guid householdId,
        bool activeOnly,
        bool includeCompletedItems,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ShoppingLists.AsNoTracking()
            .Where(x => x.HouseholdId == householdId);
        if (activeOnly)
        {
            query = query.Where(x => x.Status == ShoppingListStatus.Active);
        }

        return await query
            .Include(x => x.Items.Where(item => includeCompletedItems || item.Status == ShoppingListItemStatus.Active))
            .OrderByDescending(x => x.Status == ShoppingListStatus.Active)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<ShoppingList?> GetListAsync(
        Guid householdId,
        Guid listId,
        bool includeCompletedItems,
        CancellationToken cancellationToken) =>
        dbContext.ShoppingLists.AsNoTracking()
            .Include(x => x.Items.Where(item => includeCompletedItems || item.Status == ShoppingListItemStatus.Active))
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Id == listId, cancellationToken);

    public Task<ShoppingList?> GetListForUpdateAsync(
        Guid householdId,
        Guid listId,
        CancellationToken cancellationToken) =>
        dbContext.ShoppingLists.SingleOrDefaultAsync(
            x => x.HouseholdId == householdId && x.Id == listId,
            cancellationToken);

    public async Task AddListAsync(ShoppingList list, CancellationToken cancellationToken)
    {
        dbContext.ShoppingLists.Add(list);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ShoppingListItem?> GetItemAsync(
        Guid householdId,
        Guid listId,
        Guid itemId,
        CancellationToken cancellationToken) =>
        dbContext.ShoppingListItems
            .Include(x => x.ShoppingList)
            .SingleOrDefaultAsync(
                x => x.Id == itemId && x.ShoppingListId == listId && x.ShoppingList.HouseholdId == householdId,
                cancellationToken);

    public async Task AddItemAsync(ShoppingListItem item, CancellationToken cancellationToken)
    {
        dbContext.ShoppingListItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShoppingListItem>> GetActiveItemsAsync(
        Guid householdId,
        CancellationToken cancellationToken) =>
        await dbContext.ShoppingListItems.AsNoTracking()
            .Include(x => x.ShoppingList)
            .Where(x =>
                x.ShoppingList.HouseholdId == householdId &&
                x.ShoppingList.Status == ShoppingListStatus.Active &&
                x.Status == ShoppingListItemStatus.Active)
            .ToListAsync(cancellationToken);

    public Task<bool> MatchExistsAsync(Guid itemId, Guid expenseLineId, CancellationToken cancellationToken) =>
        dbContext.ShoppingListExpenseMatches.AnyAsync(
            x => x.ShoppingListItemId == itemId && x.ExpenseLineId == expenseLineId,
            cancellationToken);

    public async Task AddMatchesAsync(
        IReadOnlyCollection<ShoppingListExpenseMatch> matches,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0) return;
        dbContext.ShoppingListExpenseMatches.AddRange(matches);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShoppingListExpenseMatch>> GetMatchesAsync(
        Guid householdId,
        ShoppingListMatchStatus? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ShoppingListExpenseMatches.AsNoTracking()
            .Include(x => x.ShoppingListItem).ThenInclude(x => x.ShoppingList)
            .Where(x => x.ShoppingListItem.ShoppingList.HouseholdId == householdId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }
        return await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<ShoppingListExpenseMatch?> GetMatchAsync(
        Guid householdId,
        Guid matchId,
        CancellationToken cancellationToken) =>
        dbContext.ShoppingListExpenseMatches
            .Include(x => x.ShoppingListItem).ThenInclude(x => x.ShoppingList)
            .SingleOrDefaultAsync(
                x => x.Id == matchId && x.ShoppingListItem.ShoppingList.HouseholdId == householdId,
                cancellationToken);

    public async Task RejectOtherPendingMatchesAsync(
        Guid itemId,
        Guid exceptMatchId,
        long userId,
        DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken)
    {
        var matches = await dbContext.ShoppingListExpenseMatches
            .Where(x =>
                x.ShoppingListItemId == itemId &&
                x.Id != exceptMatchId &&
                x.Status == ShoppingListMatchStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var match in matches)
        {
            match.Status = ShoppingListMatchStatus.Rejected;
            match.ReviewedByUserId = userId;
            match.ReviewedAtUtc = reviewedAtUtc;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
