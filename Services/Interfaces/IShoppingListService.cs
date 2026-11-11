namespace otw.fings.api.management.Services.Interfaces;

public interface IShoppingListService
{
    Task<IReadOnlyList<ShoppingListResponse>> GetListsAsync(Guid householdId, long userId, bool activeOnly, bool includeCompletedItems, CancellationToken cancellationToken);
    Task<ShoppingListResponse> GetListAsync(Guid householdId, Guid listId, long userId, bool includeCompletedItems, CancellationToken cancellationToken);
    Task<ShoppingListResponse> CreateListAsync(Guid householdId, long userId, CreateShoppingListRequest request, CancellationToken cancellationToken);
    Task<ShoppingListResponse> UpdateListAsync(Guid householdId, Guid listId, long userId, UpdateShoppingListRequest request, CancellationToken cancellationToken);
    Task ArchiveListAsync(Guid householdId, Guid listId, long userId, CancellationToken cancellationToken);
    Task<ShoppingListItemResponse> AddItemAsync(Guid householdId, Guid listId, long userId, AddShoppingListItemRequest request, CancellationToken cancellationToken);
    Task<AddShoppingListItemFromImageResponse> AddItemFromImageAsync(Guid householdId, Guid listId, long userId, Stream image, string contentType, long length, CancellationToken cancellationToken);
    Task<ShoppingListItemResponse> UpdateItemAsync(Guid householdId, Guid listId, Guid itemId, long userId, UpdateShoppingListItemRequest request, CancellationToken cancellationToken);
    Task RemoveItemAsync(Guid householdId, Guid listId, Guid itemId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ShoppingListExpenseMatchResponse>> GetExpenseMatchesAsync(Guid householdId, long userId, ShoppingListMatchStatus? status, CancellationToken cancellationToken);
    Task<ShoppingListExpenseMatchResponse> ReviewExpenseMatchAsync(Guid householdId, Guid matchId, long userId, ReviewShoppingListExpenseMatchRequest request, CancellationToken cancellationToken);
}

public interface IShoppingListExpenseMatcher
{
    Task DetectMatchesAsync(Expense expense, CancellationToken cancellationToken);
}

public interface IProductImageRecognizer
{
    Task<ProductRecognitionResponse> RecognizeAsync(Stream image, string contentType, long length, CancellationToken cancellationToken);
}
