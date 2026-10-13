using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
namespace Cafe.Api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>Lists active menu items, optionally filtered by category.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryDto>>> GetMenu(
        [FromQuery] ProductCategory? category, CancellationToken ct)
        => Ok(await products.GetMenuAsync(category, ct));

    /// <summary>Gets one menu item with all of its variants.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> GetById(int id, CancellationToken ct)
    {
        var product = await products.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Current availability and price for cart lines. Variants that no longer exist come back unavailable.</summary>
    [HttpPost("check")]
    [ProducesResponseType<IReadOnlyList<VariantCheckDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VariantCheckDto>>> Check(
        VariantCheckRequest request, CancellationToken ct)
        => Ok(await products.CheckVariantsAsync(request.VariantIds, ct));
}