using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
namespace Cafe.Api.Services;

internal static class AdminMapping
{
    public static AdminProductDto ToAdminDto(this Product p) => new(
        p.Id, p.Name, p.Description, p.ImageUrl, p.Category, p.IsActive, p.CreatedAt,
        p.Variants
            .OrderBy(v => v.SortOrder)
            .Select(v => new AdminVariantDto(v.Id, v.Name, v.Price, v.IsAvailable, v.SortOrder))
            .ToList());
}