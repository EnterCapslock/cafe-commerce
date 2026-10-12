using Cafe.Api.Data;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Entities;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Cafe.Api.Services;

public class AdminProductService(
    AppDbContext db,
    IImageStorage images,
    ILogger<AdminProductService> logger) : IAdminProductService
{
    public const long MaxImageBytes = 4 * 1024 * 1024;

    public async Task<IReadOnlyList<AdminProductDto>> ListAsync(CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .ToListAsync(ct);

        return products.Select(p => p.ToAdminDto()).ToList();
    }

    public async Task<AdminProductDto?> GetAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product?.ToAdminDto();
    }

    public async Task<AdminProductDto> CreateAsync(ProductInput input, CancellationToken ct)
    {
        var product = new Product
        {
            Name = input.Name.Trim(),
            Description = input.Description.Trim(),
            Category = input.Category,
            IsActive = input.IsActive
        };

        for (var i = 0; i < input.Variants.Count; i++)
        {
            var v = input.Variants[i];
            product.Variants.Add(new ProductVariant
            {
                Name = v.Name.Trim(),
                Price = v.Price,
                IsAvailable = v.IsAvailable,
                SortOrder = i
            });
        }

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} created with {Variants} variants", product.Id, product.Variants.Count);
        return product.ToAdminDto();
    }

    public async Task<ServiceResult<AdminProductDto>> UpdateAsync(int id, ProductInput input, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return ServiceResult<AdminProductDto>.NotFound();

        var existing = product.Variants.ToDictionary(v => v.Id);

        // Every supplied id must belong to this product.
        foreach (var v in input.Variants.Where(v => v.Id is not null))
        {
            if (!existing.ContainsKey(v.Id!.Value))
                return ServiceResult<AdminProductDto>.Invalid($"Variant {v.Id} doesn't belong to this item.");
        }

        // Variants the admin left out are removed, unless they appear in past orders.
        var keep = input.Variants.Where(v => v.Id is not null).Select(v => v.Id!.Value).ToHashSet();
        var removing = product.Variants.Where(v => !keep.Contains(v.Id)).ToList();
        if (removing.Count > 0)
        {
            var removingIds = removing.Select(v => v.Id).ToList();
            var ordered = await db.OrderItems
                .Where(i => removingIds.Contains(i.ProductVariantId))
                .Select(i => i.ProductVariantId)
                .Distinct()
                .ToListAsync(ct);

            if (ordered.Count > 0)
            {
                var names = string.Join(", ", removing.Where(v => ordered.Contains(v.Id)).Select(v => $"'{v.Name}'"));
                return ServiceResult<AdminProductDto>.Conflict(
                    $"{names} has past orders and can't be removed. Mark it unavailable instead.");
            }
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Removals first and saved on their own, so a removed name can be reused by a new variant.
            if (removing.Count > 0)
            {
                db.ProductVariants.RemoveRange(removing);
                await db.SaveChangesAsync(ct);
            }

            product.Name = input.Name.Trim();
            product.Description = input.Description.Trim();
            product.Category = input.Category;
            product.IsActive = input.IsActive;

            for (var i = 0; i < input.Variants.Count; i++)
            {
                var v = input.Variants[i];
                if (v.Id is { } variantId)
                {
                    var entity = existing[variantId];
                    entity.Name = v.Name.Trim();
                    entity.Price = v.Price;
                    entity.IsAvailable = v.IsAvailable;
                    entity.SortOrder = i;
                }
                else
                {
                    product.Variants.Add(new ProductVariant
                    {
                        Name = v.Name.Trim(),
                        Price = v.Price,
                        IsAvailable = v.IsAvailable,
                        SortOrder = i
                    });
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ServiceResult<AdminProductDto>.Conflict("Variant names must be unique within an item.");
        }

        logger.LogInformation("Product {ProductId} updated", id);
        return ServiceResult<AdminProductDto>.Ok(product.ToAdminDto());
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return ServiceResult<bool>.NotFound();

        var hasOrders = await db.OrderItems.AnyAsync(i => i.ProductVariant.ProductId == id, ct);
        if (hasOrders)
        {
            return ServiceResult<bool>.Conflict(
                "This item has past orders, so it can't be deleted. Archive it instead by setting it to inactive.");
        }

        var publicId = product.ImagePublicId;
        db.Products.Remove(product); // variants are removed by the cascade
        await db.SaveChangesAsync(ct);

        if (publicId is not null) await images.DeleteAsync(publicId);
        logger.LogInformation("Product {ProductId} deleted", id);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<AdminProductDto>> SetImageAsync(
        int id, Stream content, long length, CancellationToken ct)
    {
        if (!images.IsConfigured)
            return ServiceResult<AdminProductDto>.Unavailable("Image uploads aren't configured on this server.");

        var product = await db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return ServiceResult<AdminProductDto>.NotFound();

        if (length <= 0 || length > MaxImageBytes)
        {
            return ServiceResult<AdminProductDto>.Invalid(
                $"Images must be between 1 byte and {MaxImageBytes / 1024 / 1024} MB.");
        }

        var kind = await ImageSniffer.DetectAsync(content, ct);
        if (kind is null)
            return ServiceResult<AdminProductDto>.Invalid("Please upload a JPEG, PNG, or WebP image.");

        StoredImage stored;
        try
        {
            stored = await images.UploadAsync(content, $"product-{id}.{kind}", ct);
        }
        catch (ImageStorageException ex)
        {
            logger.LogError(ex, "Image upload failed for product {ProductId}", id);
            return ServiceResult<AdminProductDto>.Unavailable("We couldn't upload the image. Please try again.");
        }

        var previous = product.ImagePublicId;
        product.ImageUrl = stored.Url;
        product.ImagePublicId = stored.PublicId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await images.DeleteAsync(stored.PublicId); // don't leave an orphan behind
            throw;
        }

        if (previous is not null) await images.DeleteAsync(previous);
        return ServiceResult<AdminProductDto>.Ok(product.ToAdminDto());
    }

    public async Task<ServiceResult<AdminProductDto>> RemoveImageAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return ServiceResult<AdminProductDto>.NotFound();

        var publicId = product.ImagePublicId;
        product.ImageUrl = null;
        product.ImagePublicId = null;
        await db.SaveChangesAsync(ct);

        if (publicId is not null) await images.DeleteAsync(publicId);
        return ServiceResult<AdminProductDto>.Ok(product.ToAdminDto());
    }
}