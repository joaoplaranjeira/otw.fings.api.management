using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using otw.fings.api.management.Infrastructure.Repositories.Interfaces;
using otw.fings.api.management.Services.Interfaces;

namespace otw.fings.api.management.Services;

public sealed partial class ShoppingListService(
    IShoppingListRepository repository,
    IFinanceRepository financeRepository,
    IProductImageRecognizer imageRecognizer,
    TimeProvider? timeProvider = null) : IShoppingListService, IShoppingListExpenseMatcher
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<IReadOnlyList<ShoppingListResponse>> GetListsAsync(
        Guid householdId,
        long userId,
        bool activeOnly,
        bool includeCompletedItems,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        return (await repository.GetListsAsync(householdId, activeOnly, includeCompletedItems, cancellationToken))
            .Select(Map).ToArray();
    }

    public async Task<ShoppingListResponse> GetListAsync(
        Guid householdId,
        Guid listId,
        long userId,
        bool includeCompletedItems,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        var list = await repository.GetListAsync(householdId, listId, includeCompletedItems, cancellationToken)
            ?? throw new NotFoundException("Lista de compras não encontrada.");
        return Map(list);
    }

    public async Task<ShoppingListResponse> CreateListAsync(
        Guid householdId,
        long userId,
        CreateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var list = new ShoppingList
        {
            HouseholdId = householdId,
            CreatedByUserId = userId,
            Name = RequiredText(request.Name, "O nome da lista é obrigatório.")
        };
        await repository.AddListAsync(list, cancellationToken);
        return Map(list);
    }

    public async Task<ShoppingListResponse> UpdateListAsync(
        Guid householdId,
        Guid listId,
        long userId,
        UpdateShoppingListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (!Enum.IsDefined(request.Status)) throw new ValidationException("O estado da lista não é válido.");
        var existing = await GetTrackedListAsync(householdId, listId, cancellationToken);
        existing.Name = RequiredText(request.Name, "O nome da lista é obrigatório.");
        existing.Status = request.Status;
        await repository.SaveChangesAsync(cancellationToken);
        return await GetListAsync(householdId, listId, userId, true, cancellationToken);
    }

    public async Task ArchiveListAsync(Guid householdId, Guid listId, long userId, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var list = await GetTrackedListAsync(householdId, listId, cancellationToken);
        list.Status = ShoppingListStatus.Archived;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShoppingListItemResponse> AddItemAsync(
        Guid householdId,
        Guid listId,
        long userId,
        AddShoppingListItemRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        await EnsureActiveListAsync(householdId, listId, cancellationToken);
        ValidateQuantity(request.Quantity);
        ValidateExpectedPrice(request.ExpectedPrice);
        var name = RequiredText(request.ProductName, "O nome do produto é obrigatório.");
        var item = new ShoppingListItem
        {
            ShoppingListId = listId,
            AddedByUserId = userId,
            ProductName = name,
            NormalizedProductName = Normalize(name),
            Brand = EmptyToNull(request.Brand),
            Quantity = request.Quantity,
            Unit = EmptyToNull(request.Unit),
            ExpectedPrice = request.ExpectedPrice,
            Notes = EmptyToNull(request.Notes)
        };
        await repository.AddItemAsync(item, cancellationToken);
        return Map(item);
    }

    public async Task<AddShoppingListItemFromImageResponse> AddItemFromImageAsync(
        Guid householdId,
        Guid listId,
        long userId,
        Stream image,
        string contentType,
        long length,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        await EnsureActiveListAsync(householdId, listId, cancellationToken);
        var recognition = await imageRecognizer.RecognizeAsync(image, contentType, length, cancellationToken);
        var quantity = recognition.SuggestedQuantity is > 0 ? recognition.SuggestedQuantity : null;
        var item = new ShoppingListItem
        {
            ShoppingListId = listId,
            AddedByUserId = userId,
            ProductName = recognition.ProductName.Trim(),
            NormalizedProductName = Normalize(recognition.ProductName),
            Brand = EmptyToNull(recognition.Brand),
            Variant = EmptyToNull(recognition.Variant),
            Quantity = quantity,
            Unit = EmptyToNull(recognition.SuggestedUnit),
            Source = ShoppingListItemSource.ProductPhoto,
            DetectionConfidence = recognition.Confidence
        };
        await repository.AddItemAsync(item, cancellationToken);
        return new(Map(item), recognition);
    }

    public async Task<ShoppingListItemResponse> UpdateItemAsync(
        Guid householdId,
        Guid listId,
        Guid itemId,
        long userId,
        UpdateShoppingListItemRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        ValidateQuantity(request.Quantity);
        ValidateExpectedPrice(request.ExpectedPrice);
        var item = await repository.GetItemAsync(householdId, listId, itemId, cancellationToken)
            ?? throw new NotFoundException("Produto não encontrado na lista.");
        if (item.Status != ShoppingListItemStatus.Active)
        {
            throw new ConflictException("Apenas produtos ativos podem ser alterados.");
        }
        item.ProductName = RequiredText(request.ProductName, "O nome do produto é obrigatório.");
        item.NormalizedProductName = Normalize(item.ProductName);
        item.Brand = EmptyToNull(request.Brand);
        item.Quantity = request.Quantity;
        item.Unit = EmptyToNull(request.Unit);
        item.ExpectedPrice = request.ExpectedPrice;
        item.Notes = EmptyToNull(request.Notes);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task RemoveItemAsync(
        Guid householdId,
        Guid listId,
        Guid itemId,
        long userId,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        var item = await repository.GetItemAsync(householdId, listId, itemId, cancellationToken)
            ?? throw new NotFoundException("Produto não encontrado na lista.");
        if (item.Status != ShoppingListItemStatus.Active) return;
        item.Status = ShoppingListItemStatus.Removed;
        item.CompletedByUserId = userId;
        item.CompletedAtUtc = timeProvider.GetUtcNow();
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DetectMatchesAsync(Expense expense, CancellationToken cancellationToken)
    {
        if (expense.Status == FinancialRecordStatus.Cancelled || expense.Lines.Count == 0) return;
        var items = await repository.GetActiveItemsAsync(expense.HouseholdId, cancellationToken);
        if (items.Count == 0) return;

        var matches = new List<ShoppingListExpenseMatch>();
        foreach (var item in items)
        {
            var best = expense.Lines
                .Select(line => new { Line = line, Confidence = Similarity(item.NormalizedProductName, Normalize(line.Description)) })
                .OrderByDescending(x => x.Confidence)
                .FirstOrDefault();
            if (best is null || best.Confidence < 0.72m || best.Line.Amount <= 0) continue;
            if (await repository.MatchExistsAsync(item.Id, best.Line.Id, cancellationToken)) continue;
            matches.Add(new ShoppingListExpenseMatch
            {
                ShoppingListItemId = item.Id,
                ExpenseId = expense.Id,
                ExpenseLineId = best.Line.Id,
                ExpenseLineDescription = best.Line.Description,
                RequestedQuantityAtDetection = item.Quantity,
                DetectedPurchasedQuantity = best.Line.Quantity,
                Confidence = best.Confidence
            });
        }
        await repository.AddMatchesAsync(matches, cancellationToken);
    }

    public async Task<IReadOnlyList<ShoppingListExpenseMatchResponse>> GetExpenseMatchesAsync(
        Guid householdId,
        long userId,
        ShoppingListMatchStatus? status,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, false, cancellationToken);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ValidationException("O estado da correspondência não é válido.");
        return (await repository.GetMatchesAsync(householdId, status, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<ShoppingListExpenseMatchResponse> ReviewExpenseMatchAsync(
        Guid householdId,
        Guid matchId,
        long userId,
        ReviewShoppingListExpenseMatchRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(householdId, userId, true, cancellationToken);
        if (request.Decision is not ShoppingListMatchStatus.Approved and not ShoppingListMatchStatus.Rejected)
        {
            throw new ValidationException("A decisão deve ser Approved ou Rejected.");
        }
        var match = await repository.GetMatchAsync(householdId, matchId, cancellationToken)
            ?? throw new NotFoundException("Correspondência não encontrada.");
        if (match.Status != ShoppingListMatchStatus.Pending)
        {
            throw new ConflictException("A correspondência já foi revista.");
        }

        var reviewedAt = timeProvider.GetUtcNow();
        if (request.Decision == ShoppingListMatchStatus.Approved)
        {
            if (match.ShoppingListItem.Status != ShoppingListItemStatus.Active)
            {
                throw new ConflictException("O produto já não está ativo na lista.");
            }

            var purchasedQuantity = request.PurchasedQuantity ?? match.DetectedPurchasedQuantity;
            if (match.ShoppingListItem.Quantity.HasValue)
            {
                if (!purchasedQuantity.HasValue || purchasedQuantity.Value <= 0)
                {
                    throw new ValidationException(
                        "Indique uma quantidade comprada superior a zero, porque a despesa não contém uma quantidade válida.");
                }
                var appliedQuantity = purchasedQuantity.Value;
                match.ApprovedPurchasedQuantity = appliedQuantity;
                var remainingQuantity = Math.Max(0, match.ShoppingListItem.Quantity.Value - appliedQuantity);
                match.RemainingQuantity = remainingQuantity;
                match.ShoppingListItem.Quantity = remainingQuantity;
                if (remainingQuantity > 0)
                {
                    match.ShoppingListItem.Status = ShoppingListItemStatus.Active;
                    match.ShoppingListItem.CompletedByUserId = null;
                    match.ShoppingListItem.CompletedAtUtc = null;
                }
                else
                {
                    CompleteItem(match.ShoppingListItem, userId, reviewedAt);
                    await repository.RejectOtherPendingMatchesAsync(
                        match.ShoppingListItemId, match.Id, userId, reviewedAt, cancellationToken);
                }
            }
            else
            {
                match.ApprovedPurchasedQuantity = purchasedQuantity is > 0 ? purchasedQuantity : null;
                match.RemainingQuantity = null;
                CompleteItem(match.ShoppingListItem, userId, reviewedAt);
                await repository.RejectOtherPendingMatchesAsync(
                    match.ShoppingListItemId, match.Id, userId, reviewedAt, cancellationToken);
            }
        }
        match.Status = request.Decision;
        match.ReviewedByUserId = userId;
        match.ReviewedAtUtc = reviewedAt;
        await repository.SaveChangesAsync(cancellationToken);
        return Map(match);
    }

    private async Task<ShoppingList> GetTrackedListAsync(Guid householdId, Guid listId, CancellationToken cancellationToken)
    {
        return await repository.GetListForUpdateAsync(householdId, listId, cancellationToken)
            ?? throw new NotFoundException("Lista de compras não encontrada.");
    }

    private async Task EnsureActiveListAsync(Guid householdId, Guid listId, CancellationToken cancellationToken)
    {
        var list = await repository.GetListAsync(householdId, listId, false, cancellationToken)
            ?? throw new NotFoundException("Lista de compras não encontrada.");
        if (list.Status != ShoppingListStatus.Active) throw new ConflictException("A lista de compras está arquivada.");
    }

    private async Task EnsureMemberAsync(Guid householdId, long userId, bool write, CancellationToken cancellationToken)
    {
        var role = await financeRepository.GetMemberRoleAsync(householdId, userId, cancellationToken)
            ?? throw new ForbiddenException("O utilizador não pertence ao agregado indicado.");
        if (write && role == HouseholdRole.Viewer)
        {
            throw new ForbiddenException("O utilizador não tem permissões de escrita neste agregado.");
        }
    }

    internal static decimal Similarity(string normalizedProduct, string normalizedExpenseLine)
    {
        if (normalizedProduct.Length == 0 || normalizedExpenseLine.Length == 0) return 0;
        if (normalizedProduct == normalizedExpenseLine) return 1;
        var productTokens = normalizedProduct.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var lineTokens = normalizedExpenseLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        if (productTokens.IsSubsetOf(lineTokens)) return productTokens.Count == 1 ? 0.82m : 0.94m;
        if (lineTokens.IsSubsetOf(productTokens)) return 0.9m;
        var intersection = productTokens.Intersect(lineTokens).Count();
        var union = productTokens.Union(lineTokens).Count();
        return union == 0 ? 0 : Math.Round((decimal)intersection / union, 4);
    }

    internal static string Normalize(string value)
    {
        var decomposed = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
            }
        }
        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static void ValidateQuantity(decimal? quantity)
    {
        if (quantity is <= 0) throw new ValidationException("Quando indicada, a quantidade deve ser superior a zero.");
    }

    private static void ValidateExpectedPrice(decimal? expectedPrice)
    {
        if (expectedPrice is < 0) throw new ValidationException("O preço expectável não pode ser negativo.");
    }

    private static string RequiredText(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void CompleteItem(ShoppingListItem item, long userId, DateTimeOffset completedAtUtc)
    {
        item.Status = ShoppingListItemStatus.Purchased;
        item.CompletedByUserId = userId;
        item.CompletedAtUtc = completedAtUtc;
    }

    private static ShoppingListResponse Map(ShoppingList list) => new(
        list.Id,
        list.Name,
        list.Status,
        list.CreatedAtUtc,
        list.Items.Count(x => x.Status == ShoppingListItemStatus.Active),
        list.Items.OrderBy(x => x.Status).ThenBy(x => x.ProductName).Select(Map).ToArray());

    private static ShoppingListItemResponse Map(ShoppingListItem item) => new(
        item.Id, item.ProductName, item.Brand, item.Variant, item.Quantity, item.Unit, item.ExpectedPrice, item.Notes,
        item.Source, item.DetectionConfidence, item.Status, item.CreatedAtUtc, item.CompletedAtUtc);

    private static ShoppingListExpenseMatchResponse Map(ShoppingListExpenseMatch match) => new(
        match.Id,
        match.ShoppingListItem.ShoppingListId,
        match.ShoppingListItem.ShoppingList.Name,
        match.ShoppingListItemId,
        match.ShoppingListItem.ProductName,
        match.ExpenseId,
        match.ExpenseLineId,
        match.ExpenseLineDescription,
        match.RequestedQuantityAtDetection,
        match.DetectedPurchasedQuantity,
        match.ApprovedPurchasedQuantity,
        match.RemainingQuantity,
        match.Confidence,
        match.Status,
        match.CreatedAtUtc,
        match.ReviewedAtUtc);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
