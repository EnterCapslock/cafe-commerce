using System.Text.RegularExpressions;
using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Services.Orders;

public class OrderService(
    AppDbContext db,
    TimeProvider time,
    IPaymentGateway gateway,
    IOptions<OrderOptions> options,
    ILogger<OrderService> logger) : IOrderService
{
    private readonly OrderOptions _opts = options.Value;

    public async Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request, int? userId, CancellationToken ct)
    {
        // 1. Load every requested variant with its product, in one query.
        var ids = request.Items.Select(i => i.VariantId).ToList();
        var variants = await db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        // 2. Business checks: the variant must exist, be available, and its product active.
        var errors = new List<string>();
        foreach (var line in request.Items)
        {
            if (!variants.TryGetValue(line.VariantId, out var v))
                errors.Add("An item in your cart is no longer on the menu.");
            else if (!v.IsAvailable || !v.Product.IsActive)
                errors.Add($"{v.Product.Name} ({v.Name}) is currently unavailable.");
        }
        if (errors.Count > 0) return CreateOrderResult.Fail(errors);

        // 3. A valid token for a since-deleted user would violate the FK, so treat it as a guest.
        if (userId is { } uid && !await db.Users.AnyAsync(u => u.Id == uid, ct))
            userId = null;

        // 4. Store phone numbers as 09XXXXXXXXX
        var cleanPhone = Regex.Replace(request.CustomerPhone, @"[\s\-]", "");
        if (cleanPhone.StartsWith("+63"))
            cleanPhone = "0" + cleanPhone[3..];

        // 5. Build the order. Prices come from the database, never from the client.
        var now = time.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            UserId = userId,
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim().ToLowerInvariant(),
            CustomerPhone = cleanPhone,
            PickupTime = request.PickupTime.UtcDateTime,
            Status = OrderStatus.Pending,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Unpaid,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var line in request.Items)
        {
            var v = variants[line.VariantId];
            order.Items.Add(new OrderItem
            {
                ProductVariantId = v.Id,
                ProductName = v.Product.Name,   // snapshots
                VariantName = v.Name,
                Quantity = line.Quantity,
                PriceAtPurchase = v.Price
            });
        }
        order.TotalAmount = order.Items.Sum(i => i.PriceAtPurchase * i.Quantity);

        // 6. Large orders can't be left to a "pay later" promise.
        if (order.PaymentMethod == PaymentMethod.PayAtCounter && order.TotalAmount > _opts.MaxCounterPaymentAmount)
        {
            return CreateOrderResult.Fail(
            [
                $"Orders over ₱{_opts.MaxCounterPaymentAmount:N0} can't be paid at the counter. Please pay online."
            ]);
        }

        // 7. Online: create the hosted checkout BEFORE saving, so a gateway outage leaves no orphan order.
        if (order.PaymentMethod == PaymentMethod.Online)
        {
            try
            {
                var session = await gateway.CreateCheckoutAsync(order, ct);
                order.PaymentReference = session.Id;
                order.CheckoutUrl = session.Url;
            }
            catch (PaymentGatewayException ex)
            {
                logger.LogError(ex, "Online payment unavailable for new order {OrderId}", order.Id);
                return CreateOrderResult.GatewayDown();
            }
        }

        // 8. One SaveChanges = one transaction: the order and all its items, or nothing.
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Order {OrderId} created ({Lines} lines, total {Total}, {Method}, user {UserId})",
            order.Id, order.Items.Count, order.TotalAmount, order.PaymentMethod, userId);

        return CreateOrderResult.Ok(order.ToDto());
    }

    public async Task<OrderDto?> GetByIdAsync(Guid id, int? requesterId, bool isAdmin, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null) return null;

        // Guest orders (UserId == null) are reachable by their unguessable ID.
        // Orders owned by an account are private to that account and to admins.
        if (order.UserId is not null && order.UserId != requesterId && !isAdmin)
            return null;

        return order.ToDto();
    }

    public async Task<IReadOnlyList<OrderDto>> GetForUserAsync(int userId, CancellationToken ct)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        return orders.Select(o => o.ToDto()).ToList();
    }

    public async Task<bool> MarkPaidAsync(Guid orderId, string checkoutSessionId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;

        // A single conditional UPDATE: atomic, and a duplicate delivery matches zero rows.
        var rows = await db.Orders
            .Where(o => o.Id == orderId
                     && o.PaymentMethod == PaymentMethod.Online
                     && o.PaymentReference == checkoutSessionId
                     && o.PaymentStatus != PaymentStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(o => o.PaidAt, (DateTime?)now)
                .SetProperty(o => o.UpdatedAt, now), ct);

        if (rows == 0)
        {
            logger.LogInformation(
                "Ignored payment event for order {OrderId}: unknown, mismatched, or already paid", orderId);
            return false;
        }

        if (await db.Orders.AnyAsync(o => o.Id == orderId && o.Status == OrderStatus.Cancelled, ct))
            logger.LogWarning("Order {OrderId} was paid after it was cancelled. A refund is required.", orderId);

        logger.LogInformation("Order {OrderId} marked as paid", orderId);
        return true;
    }
}