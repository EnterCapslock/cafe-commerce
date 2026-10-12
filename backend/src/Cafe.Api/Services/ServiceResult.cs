namespace Cafe.Api.Services;

public enum ServiceStatus { Ok, NotFound, Invalid, Conflict, Unavailable }

public sealed record ServiceResult<T>(ServiceStatus Status, T? Value = default, string? Message = null)
{
    public bool Succeeded => Status == ServiceStatus.Ok;

    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value);
    public static ServiceResult<T> NotFound() => new(ServiceStatus.NotFound);
    public static ServiceResult<T> Invalid(string message) => new(ServiceStatus.Invalid, default, message);
    public static ServiceResult<T> Conflict(string message) => new(ServiceStatus.Conflict, default, message);
    public static ServiceResult<T> Unavailable(string message) => new(ServiceStatus.Unavailable, default, message);
}