namespace otw.fings.api.management.Infrastructure.Repositories.Interfaces;

public interface IShoppingListRepository
{
    Task<IReadOnlyList<ShoppingList>> GetListsAsync(Guid householdId, bool activeOnly, bool includeCompletedItems, CancellationToken cancellationToken);
    Task<ShoppingList?> GetListAsync(Guid householdId, Guid listId, bool includeCompletedItems, CancellationToken cancellationToken);
    Task<ShoppingList?> GetListForUpdateAsync(Guid householdId, Guid listId, CancellationToken cancellationToken);
    Task AddListAsync(ShoppingList list, CancellationToken cancellationToken);
    Task<ShoppingListItem?> GetItemAsync(Guid householdId, Guid listId, Guid itemId, CancellationToken cancellationToken);
    Task AddItemAsync(ShoppingListItem item, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShoppingListItem>> GetActiveItemsAsync(Guid householdId, CancellationToken cancellationToken);
    Task<bool> MatchExistsAsync(Guid itemId, Guid expenseLineId, CancellationToken cancellationToken);
    Task AddMatchesAsync(IReadOnlyCollection<ShoppingListExpenseMatch> matches, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShoppingListExpenseMatch>> GetMatchesAsync(Guid householdId, ShoppingListMatchStatus? status, CancellationToken cancellationToken);
    Task<ShoppingListExpenseMatch?> GetMatchAsync(Guid householdId, Guid matchId, CancellationToken cancellationToken);
    Task RejectOtherPendingMatchesAsync(Guid itemId, Guid exceptMatchId, long userId, DateTimeOffset reviewedAtUtc, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
