using Cafe.Api.Data;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Cafe.Api.Services.Orders;

public class OrderLookupService(AppDbContext db, LookupThrottle throttle, ILogger<OrderLookupService> logger)
    : IOrderLookupService
{
    public async Task<OrderLookupResult> FindAsync(string code, string email, CancellationToken ct)
    {
        var normalizedCode = OrderCode.Normalize(code);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedCode is null) return OrderLookupResult.NotFound;

        if (!throttle.TryAcquire(normalizedEmail))
        {
            logger.LogWarning("Order lookup throttled");   // never log the email itself
            return OrderLookupResult.TooManyAttempts;
        }

        // Only guest orders can be found this way. Orders that belong to an account stay private to it.
        var id = await db.Orders
            .AsNoTracking()
            .Where(o => o.UserId == null
                     && o.CustomerEmail == normalizedEmail
                     && EF.Functions.ILike(o.Id.ToString(), $"{normalizedCode}%"))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(ct);

        return id is { } found ? OrderLookupResult.Found(found) : OrderLookupResult.NotFound;
    }
}