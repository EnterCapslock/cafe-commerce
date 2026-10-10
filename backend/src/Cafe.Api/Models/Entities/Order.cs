using Cafe.Api.Models.Enums;
namespace Cafe.Api.Models.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? UserId { get; set; }                   // null = guest checkout
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime PickupTime { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayAtCounter;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? PaymentReference { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}