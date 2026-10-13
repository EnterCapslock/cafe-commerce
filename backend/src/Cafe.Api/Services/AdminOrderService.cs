using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Cafe.Api.Services;

public class AdminOrderService(AppDbContext db, TimeProvider time, ILogger<AdminOrderService> logger)
    : IAdminOrderService
{
    public async Task<IReadOnlyList<OrderDto>> ListAsync(
        OrderStatus? status, string? search, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();

        query = status is { } s
            ? query.Where(o => o.Status == s)
            : query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);

        // Unpaid online orders aren't actionable yet. Cancelled history still shows them (as expired).
        if (status != OrderStatus.Cancelled) query = query.VisibleToKitchen();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim();
            var term = EscapeLike(trimmed[..Math.Min(trimmed.Length, 50)]);
            query = query.Where(o =>
                EF.Functions.ILike(o.CustomerName, $"%{term}%")
                || EF.Functions.ILike(o.CustomerEmail, $"%{term}%")
                || EF.Functions.ILike(o.CustomerPhone, $"%{term}%")
                || EF.Functions.ILike(o.Id.ToString(), $"{term}%")); // the 8-character order code
        }

        query = status is OrderStatus.Completed or OrderStatus.Cancelled
            ? query.OrderByDescending(o => o.UpdatedAt)
            : query.OrderBy(o => o.PickupTime).ThenBy(o => o.CreatedAt);   // soonest pickup first

        var orders = await query.Take(limit).ToListAsync(ct);
        return orders.Select(o => o.ToDto()).ToList();
    }

    public async Task<ServiceResult<OrderDto>> UpdateStatusAsync(Guid id, OrderStatus target, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return ServiceResult<OrderDto>.NotFound();

        // Check 1: only the documented moves are allowed (Completed and Cancelled are final).
        if (!OrderStatusRules.CanMove(order.Status, target))
        {
            return ServiceResult<OrderDto>.Conflict(
                $"An order that is {order.Status} can't be changed to {target}.");
        }

        // Check 2: an online order must be paid before it can advance. Cancelling is always allowed.
        if (order.PaymentMethod == PaymentMethod.Online
            && order.PaymentStatus != PaymentStatus.Paid
            && target != OrderStatus.Cancelled)
        {
            return ServiceResult<OrderDto>.Conflict("This order hasn't been paid yet.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var collectPayment = target == OrderStatus.Completed && order.PaymentMethod == PaymentMethod.PayAtCounter;

        // Check 3: the update only applies if the status is still what we just read. If another
        // barista changed the order in the meantime, zero rows match and we report it instead of
        // overwriting their change.
        var matching = db.Orders.Where(o => o.Id == id && o.Status == order.Status);

        var rows = collectPayment
            ? await matching.ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, target)
                .SetProperty(o => o.UpdatedAt, now)
                .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)   // cash or card taken at the counter
                .SetProperty(o => o.PaidAt, (DateTime?)now), ct)
            : await matching.ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, target)
                .SetProperty(o => o.UpdatedAt, now), ct);

        if (rows == 0)
        {
            return ServiceResult<OrderDto>.Conflict(
                "This order was just updated by someone else. Refresh and try again.");
        }

        if (target == OrderStatus.Cancelled && order.PaymentStatus == PaymentStatus.Paid)
            logger.LogWarning("Paid order {OrderId} was cancelled. A refund is required.", id);

        logger.LogInformation("Order {OrderId}: {From} -> {To}", id, order.Status, target);

        var updated = await db.Orders.AsNoTracking().Include(o => o.Items).FirstAsync(o => o.Id == id, ct);
        return ServiceResult<OrderDto>.Ok(updated.ToDto());
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}