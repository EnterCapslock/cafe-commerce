using Cafe.Api.Data;
using Cafe.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Services;

public sealed class OrderCleanupService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<OrderOptions> options,
    ILogger<OrderCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(o.CleanupIntervalSeconds), time);

        do
        {
            try { await RunOnceAsync(o, stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Order cleanup failed; will retry on the next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(OrderOptions o, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = time.GetUtcNow().UtcDateTime;
        var unpaidCutoff = now.AddMinutes(-o.UnpaidExpiryMinutes);
        var noShowCutoff = now.AddMinutes(-o.NoShowMinutes);

        // Online orders that were never paid. The WHERE includes PaymentStatus = Unpaid,
        // so a payment landing at the same moment can't be overwritten.
        var expired = await db.Orders
            .Where(x => x.PaymentMethod == PaymentMethod.Online
                     && x.PaymentStatus == PaymentStatus.Unpaid
                     && x.Status == OrderStatus.Pending
                     && x.CreatedAt < unpaidCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PaymentStatus, PaymentStatus.Expired)
                .SetProperty(x => x.Status, OrderStatus.Cancelled)
                .SetProperty(x => x.UpdatedAt, now), ct);

        // Pay-at-counter orders nobody collected. Paid online orders are never auto-cancelled.
        var noShows = await db.Orders
            .Where(x => x.PaymentMethod == PaymentMethod.PayAtCounter
                     && x.Status != OrderStatus.Completed
                     && x.Status != OrderStatus.Cancelled
                     && x.PickupTime < noShowCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, OrderStatus.Cancelled)
                .SetProperty(x => x.UpdatedAt, now), ct);

        if (expired > 0 || noShows > 0)
            logger.LogInformation("Cleanup: expired {Expired} unpaid online orders, cancelled {NoShows} no-shows",
                expired, noShows);
    }
}