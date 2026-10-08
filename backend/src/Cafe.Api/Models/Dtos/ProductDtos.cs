using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Dtos;

public record VariantDto(int Id, string Name, decimal Price, bool IsAvailable);

public record ProductSummaryDto(
    int Id,
    string Name,
    string Description,
    string? ImageUrl,
    ProductCategory Category,
    decimal FromPrice,
    bool IsAvailable,
    int VariantCount);

public record ProductDetailDto(
    int Id,
    string Name,
    string Description,
    string? ImageUrl,
    ProductCategory Category,
    decimal FromPrice,
    bool IsAvailable,
    IReadOnlyList<VariantDto> Variants);