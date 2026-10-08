namespace Cafe.Api.Models.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int ProductVariantId { get; set; }

    // Snapshots: order history must survive later edits to the menu
    public string ProductName { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }

    public Order Order { get; set; } = null!;
    public ProductVariant ProductVariant { get; set; } = null!;
}