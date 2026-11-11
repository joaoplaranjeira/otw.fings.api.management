using System.ComponentModel.DataAnnotations;

namespace otw.fings.api.management.DTOs;

public sealed record CreateProductTypeRequest(
    Guid CategoryId,
    Guid? SubcategoryId,
    [Required, MaxLength(250)] string Name);

public sealed record UpdateProductTypeRequest(
    Guid CategoryId,
    Guid? SubcategoryId,
    [Required, MaxLength(250)] string Name,
    bool IsActive);

public sealed record ProductTypeResponse(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    bool IsActive);
