namespace Cafe.Api.Models.Entities;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;   // "Small", "Regular", "250g"
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;      // e.g. "Large out of stock today"
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
}