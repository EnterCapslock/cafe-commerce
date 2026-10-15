using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
namespace Cafe.Api.Services.Orders;

/// <summary>Limits lookup attempts per target email, so one person's email can't be hammered from many addresses.</summary>
public sealed class LookupThrottle : IDisposable
{
    public const int PermitsPerHour = 10;

    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(key =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitsPerHour,
            Window = TimeSpan.FromHours(1)
        }));

    /// <param name="normalizedEmail">Trimmed and lower-case. It is hashed, so no address is kept in memory.</param>
    public bool TryAcquire(string normalizedEmail)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
        using var lease = _limiter.AttemptAcquire(key);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}