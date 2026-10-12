using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Dtos;

public record AdminVariantDto(int Id, string Name, decimal Price, bool IsAvailable, int SortOrder);

public record AdminProductDto(
    int Id,
    string Name,
    string Description,
    string? ImageUrl,
    ProductCategory Category,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<AdminVariantDto> Variants);

/// <param name="Id">Set for an existing variant, omitted for a new one.</param>
public record VariantInput(int? Id, string Name, decimal Price, bool IsAvailable);

public record ProductInput(
    string Name,
    string Description,
    ProductCategory Category,
    bool IsActive,
    IReadOnlyList<VariantInput> Variants);

public record UpdateOrderStatusRequest(OrderStatus Status);