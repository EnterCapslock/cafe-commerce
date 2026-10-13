using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Dtos;

public record VariantDto(int Id, string Name, decimal Price, bool IsAvailable);
public record VariantCheckRequest(IReadOnlyList<int> VariantIds);

/// <param name="Available">False if the variant is sold out, its item is archived, or it no longer exists.</param>
public record VariantCheckDto(int VariantId, bool Available, decimal? Price);

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