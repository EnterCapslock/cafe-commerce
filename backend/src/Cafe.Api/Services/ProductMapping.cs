using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
namespace Cafe.Api.Services;

internal static class ProductMapping
{
    public static ProductSummaryDto ToSummaryDto(this Product p) => new(
        p.Id, p.Name, p.Description, p.ImageUrl, p.Category,
        FromPrice(p), p.Variants.Any(v => v.IsAvailable), p.Variants.Count);

    public static ProductDetailDto ToDetailDto(this Product p) => new(
        p.Id, p.Name, p.Description, p.ImageUrl, p.Category,
        FromPrice(p), p.Variants.Any(v => v.IsAvailable),
        p.Variants
            .OrderBy(v => v.SortOrder)
            .Select(v => new VariantDto(v.Id, v.Name, v.Price, v.IsAvailable))
            .ToList());

    // Cheapest available variant; if everything is sold out, cheapest overall.
    private static decimal FromPrice(Product p)
    {
        var pool = p.Variants.Where(v => v.IsAvailable).ToList();
        if (pool.Count == 0) pool = p.Variants.ToList();
        return pool.Min(v => v.Price);
    }
}