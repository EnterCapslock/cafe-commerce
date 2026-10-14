using Cafe.Api.Extensions;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Cafe.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/products")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminProductsController(IAdminProductService products) : ControllerBase
{
    /// <summary>All menu items, including inactive ones.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminProductDto>>> List(CancellationToken ct)
        => Ok(await products.ListAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminProductDto>> GetById(int id, CancellationToken ct)
    {
        var product = await products.GetAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminProductDto>> Create(ProductInput input, CancellationToken ct)
    {
        var created = await products.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Replaces the item and syncs its variants (ids update, no id adds, missing ids remove).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminProductDto>> Update(int id, ProductInput input, CancellationToken ct)
    {
        var result = await products.UpdateAsync(id, input, ct);
        if (!result.Succeeded) return this.ToProblem(result);
        return Ok(result.Value);
    }

    /// <summary>Deletes an item that was never ordered. Items with past orders return 409: archive them instead.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await products.DeleteAsync(id, ct);
        if (!result.Succeeded) return this.ToProblem(result);
        return NoContent();
    }

    /// <summary>Uploads or replaces the item's image (multipart, field "file"; JPEG, PNG, or WebP, up to 4 MB).</summary>
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AdminProductDto>> UploadImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await products.SetImageAsync(id, stream, file.Length, ct);
        if (!result.Succeeded) return this.ToProblem(result);
        return Ok(result.Value);
    }

    [HttpDelete("{id:int}/image")]
    [ProducesResponseType<AdminProductDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminProductDto>> RemoveImage(int id, CancellationToken ct)
    {
        var result = await products.RemoveImageAsync(id, ct);
        if (!result.Succeeded) return this.ToProblem(result);
        return Ok(result.Value);
    }
}