namespace Cafe.Api.Services.Interfaces;

public enum LookupStatus { Found, NotFound, TooManyAttempts }

public sealed record OrderLookupResult(LookupStatus Status, Guid? OrderId = null)
{
    public static readonly OrderLookupResult NotFound = new(LookupStatus.NotFound);
    public static readonly OrderLookupResult TooManyAttempts = new(LookupStatus.TooManyAttempts);
    public static OrderLookupResult Found(Guid id) => new(LookupStatus.Found, id);
}

public interface IOrderLookupService
{
    Task<OrderLookupResult> FindAsync(string code, string email, CancellationToken ct);
}