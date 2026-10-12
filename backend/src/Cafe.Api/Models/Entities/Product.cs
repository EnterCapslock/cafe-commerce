using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImagePublicId { get; set; }
    public ProductCategory Category { get; set; }
    public bool IsActive { get; set; } = true;   // soft delete / hide from menu
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}