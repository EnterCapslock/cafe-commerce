using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
namespace Cafe.Api.Services.Interfaces;

public interface IAdminOrderService
{
    /// <param name="status">null = the active board (Pending, Preparing, ReadyForPickup).</param>
    Task<IReadOnlyList<OrderDto>> ListAsync(OrderStatus? status, string? search, int limit, CancellationToken ct);

    Task<ServiceResult<OrderDto>> UpdateStatusAsync(Guid id, OrderStatus target, CancellationToken ct);
}