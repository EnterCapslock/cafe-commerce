using Cafe.Api.Models.Dtos;
namespace Cafe.Api.Services.Interfaces;

public interface IAdminProductService
{
    Task<IReadOnlyList<AdminProductDto>> ListAsync(CancellationToken ct);
    Task<AdminProductDto?> GetAsync(int id, CancellationToken ct);
    Task<AdminProductDto> CreateAsync(ProductInput input, CancellationToken ct);
    Task<ServiceResult<AdminProductDto>> UpdateAsync(int id, ProductInput input, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct);
    Task<ServiceResult<AdminProductDto>> SetImageAsync(int id, Stream content, long length, CancellationToken ct);
    Task<ServiceResult<AdminProductDto>> RemoveImageAsync(int id, CancellationToken ct);
}