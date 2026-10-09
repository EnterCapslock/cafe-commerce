using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Cafe.Api.Services;

public class OrderService(AppDbContext db, TimeProvider time, ILogger<OrderService> logger) : IOrderService
{
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
                errors.Add($"Item {line.VariantId} does not exist.");
            else if (!v.IsAvailable || !v.Product.IsActive)
                errors.Add($"{v.Product.Name} ({v.Name}) is currently unavailable.");
        }
        if (errors.Count > 0) return CreateOrderResult.Fail(errors);

        // 3. A valid token for a since-deleted user would violate the FK, so treat it as a guest.
        if (userId is { } uid && !await db.Users.AnyAsync(u => u.Id == uid, ct))
            userId = null;

        // 4. Cuts off '+63' and attaches a '0'
        var cleanPhone = System.Text.RegularExpressions.Regex.Replace(request.CustomerPhone, @"[\s\-]", "");
        if (cleanPhone.StartsWith("+63"))
        {
            cleanPhone = "0" + cleanPhone[3..]; 
        }

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

        // 6. One SaveChanges = one transaction: the order and all its items, or nothing.
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Order {OrderId} created ({Lines} lines, total {Total}, user {UserId})",
            order.Id, order.Items.Count, order.TotalAmount, userId);

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
}