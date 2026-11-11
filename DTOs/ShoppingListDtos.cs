using System.ComponentModel.DataAnnotations;

namespace otw.fings.api.management.DTOs;

public sealed record CreateShoppingListRequest([Required, MaxLength(200)] string Name);

public sealed record UpdateShoppingListRequest(
    [Required, MaxLength(200)] string Name,
    ShoppingListStatus Status);

public sealed record AddShoppingListItemRequest(
    Guid ProductTypeId,
    [MaxLength(150)] string? Brand = null,
    decimal? Quantity = null,
    [MaxLength(50)] string? Unit = null,
    decimal? ExpectedPrice = null,
    [MaxLength(500)] string? Notes = null);

public sealed record UpdateShoppingListItemRequest(
    Guid? ProductTypeId = null,
    [MaxLength(250)] string? ProductTypeName = null,
    [MaxLength(150)] string? Brand = null,
    decimal? Quantity = null,
    [MaxLength(50)] string? Unit = null,
    decimal? ExpectedPrice = null,
    [MaxLength(500)] string? Notes = null);

public sealed record ShoppingListItemResponse(
    Guid Id,
    Guid? ProductTypeId,
    string ProductTypeName,
    string? Brand,
    string? Variant,
    decimal? Quantity,
    string? Unit,
    decimal? ExpectedPrice,
    string? Notes,
    ShoppingListItemSource Source,
    decimal? DetectionConfidence,
    ShoppingListItemStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ShoppingListResponse(
    Guid Id,
    string Name,
    ShoppingListStatus Status,
    DateTimeOffset CreatedAtUtc,
    int ActiveItemCount,
    IReadOnlyList<ShoppingListItemResponse> Items);

public sealed record ProductRecognitionResponse(
    string ProductTypeName,
    string? Brand,
    string? Variant,
    decimal? SuggestedQuantity,
    string? SuggestedUnit,
    decimal Confidence);

public sealed record AddShoppingListItemFromImageResponse(
    ShoppingListItemResponse Item,
    ProductRecognitionResponse Recognition);

public sealed record ShoppingListExpenseMatchResponse(
    Guid Id,
    Guid ShoppingListId,
    string ShoppingListName,
    Guid ShoppingListItemId,
    string ProductTypeName,
    Guid ExpenseId,
    Guid ExpenseLineId,
    string ExpenseLineDescription,
    decimal? RequestedQuantityAtDetection,
    decimal? DetectedPurchasedQuantity,
    decimal? ApprovedPurchasedQuantity,
    decimal? RemainingQuantity,
    decimal Confidence,
    ShoppingListMatchStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReviewedAtUtc);

public sealed record ReviewShoppingListExpenseMatchRequest(
    ShoppingListMatchStatus Decision,
    decimal? PurchasedQuantity = null);
