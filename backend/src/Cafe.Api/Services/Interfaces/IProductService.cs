using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
namespace Cafe.Api.Services.Interfaces;

public interface IProductService
{
    Task<IReadOnlyList<ProductSummaryDto>> GetMenuAsync(ProductCategory? category, CancellationToken ct);
    Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken ct);
}