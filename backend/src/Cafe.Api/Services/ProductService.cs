using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Cafe.Api.Services;

public class ProductService(AppDbContext db) : IProductService
{
    public async Task<IReadOnlyList<ProductSummaryDto>> GetMenuAsync(
        ProductCategory? category, CancellationToken ct)
    {
        var query = db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Variants.Any());

        if (category is { } c)
            query = query.Where(p => p.Category == c);

        var products = await query
            .Include(p => p.Variants)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .ToListAsync(ct);

        return products.Select(p => p.ToSummaryDto()).ToList();
    }

    public async Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && p.Variants.Any(), ct);

        return product?.ToDetailDto();
    }
}