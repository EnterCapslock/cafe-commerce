using System.Net;
using System.Net.Http.Headers;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cafe.Api.Tests.Integration;

public class AdminProductsTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Client => api.Client;

    private static ProductInput Input(string name, params VariantInput[] variants) =>
        new(name, "Created by an integration test.", ProductCategory.Merchandise, true, variants);

    private async Task<AdminProductDto> UpdateAsync(AdminProductDto product, ProductInput input) =>
        await (await api.AdminAsync(HttpMethod.Put, $"/api/admin/products/{product.Id}", input)).ReadAsync<AdminProductDto>();

    [Fact]
    public async Task Creating_an_item_stores_its_options_in_order_and_shows_it_on_the_public_menu()
    {
        var created = await api.CreateProductAsync([("  Small ", 100m), ("Medium", 120m), ("Large", 150m)]);

        Assert.Equal(["Small", "Medium", "Large"], created.Variants.Select(v => v.Name));   // names are trimmed
        Assert.Equal([0, 1, 2], created.Variants.Select(v => v.SortOrder));

        var detail = await (await Client.Call(HttpMethod.Get, $"/api/products/{created.Id}")).ReadAsync<ProductDetailDto>();
        Assert.Equal(100m, detail.FromPrice);
        Assert.Equal(["Small", "Medium", "Large"], detail.Variants.Select(v => v.Name));

        var menu = await (await Client.Call(HttpMethod.Get, "/api/products?category=Merchandise")).ReadAsync<List<ProductSummaryDto>>();
        Assert.Contains(menu, p => p.Id == created.Id);
    }

    [Fact]
    public async Task Updating_syncs_options_by_id_and_follows_the_order_in_the_request()
    {
        var product = await api.CreateProductAsync([("A", 10m), ("B", 20m), ("C", 30m)]);
        var (a, b, c) = (product.Variants[0], product.Variants[1], product.Variants[2]);

        // Keep C and A (reordered, with new prices), drop B, add D
        var updated = await UpdateAsync(product, Input(product.Name,
            new VariantInput(c.Id, "C", 33m, true),
            new VariantInput(a.Id, "A", 11m, true),
            new VariantInput(null, "D", 44m, true)));

        Assert.Equal(["C", "A", "D"], updated.Variants.Select(v => v.Name));
        Assert.Equal([0, 1, 2], updated.Variants.Select(v => v.SortOrder));
        Assert.Equal(c.Id, updated.Variants[0].Id);
        Assert.Equal(33m, updated.Variants[0].Price);
        Assert.Equal(a.Id, updated.Variants[1].Id);
        Assert.DoesNotContain(updated.Variants, v => v.Id == b.Id);
        Assert.NotEqual(0, updated.Variants[2].Id);
    }

    [Fact]
    public async Task A_removed_option_can_have_its_name_reused_in_the_same_update()
    {
        var product = await api.CreateProductAsync([("Small", 10m), ("Large", 20m)]);

        var updated = await UpdateAsync(product, Input(product.Name,
            new VariantInput(product.Variants[0].Id, "Small", 10m, true),
            new VariantInput(null, "Large", 25m, true)));     // the old "Large" is removed, a new one added

        Assert.Equal(["Small", "Large"], updated.Variants.Select(v => v.Name));
        Assert.NotEqual(product.Variants[1].Id, updated.Variants[1].Id);
        Assert.Equal(25m, updated.Variants[1].Price);
    }

    [Fact]
    public async Task An_option_that_appears_in_past_orders_cannot_be_removed()
    {
        var product = await api.CreateProductAsync([("Small", 10m), ("Large", 20m)]);
        await api.PlaceOrderAsync(api.BuildOrder(product.Variants[0].Id));

        var response = await api.AdminAsync(HttpMethod.Put, $"/api/admin/products/{product.Id}",
            Input(product.Name, new VariantInput(product.Variants[1].Id, "Large", 20m, true)));   // drops "Small"

        var problem = await response.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Contains("past orders", problem.Title);

        // Nothing changed
        var current = await (await api.AdminAsync(HttpMethod.Get, $"/api/admin/products/{product.Id}")).ReadAsync<AdminProductDto>();
        Assert.Equal(2, current.Variants.Count);

        // Marking it unavailable is the supported alternative
        var hidden = await UpdateAsync(product, Input(product.Name,
            new VariantInput(product.Variants[0].Id, "Small", 10m, false),
            new VariantInput(product.Variants[1].Id, "Large", 20m, true)));
        Assert.False(hidden.Variants[0].IsAvailable);
    }

    [Fact]
    public async Task Duplicate_names_and_options_from_another_item_are_refused()
    {
        var product = await api.CreateProductAsync([("Small", 10m)]);
        var other = await api.CreateProductAsync([("Medium", 20m)]);

        var duplicates = await api.AdminAsync(HttpMethod.Put, $"/api/admin/products/{product.Id}",
            Input(product.Name, new VariantInput(null, "Large", 10m, true), new VariantInput(null, " large", 12m, true)));
        Assert.Contains("variants", (await duplicates.ReadValidationAsync()).Errors.Keys);

        var foreign = await api.AdminAsync(HttpMethod.Put, $"/api/admin/products/{product.Id}",
            Input(product.Name, new VariantInput(other.Variants[0].Id, "Medium", 20m, true)));
        var problem = await foreign.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("doesn't belong", problem.Title);
    }

    [Fact]
    public async Task Invalid_products_are_rejected_with_field_errors()
    {
        var response = await api.AdminAsync(HttpMethod.Post, "/api/admin/products",
            Input("", new VariantInput(null, "Regular", 0m, true), new VariantInput(null, "Fancy", 1.234m, true)));

        var problem = await response.ReadValidationAsync();
        Assert.Contains("Name", problem.Errors.Keys.Select(k => char.ToUpperInvariant(k[0]) + k[1..]));
        Assert.Contains(problem.Errors.Keys, k => k.Contains("price", StringComparison.OrdinalIgnoreCase));

        var noVariants = await api.AdminAsync(HttpMethod.Post, "/api/admin/products", Input("No options"));
        Assert.Contains("variants", (await noVariants.ReadValidationAsync()).Errors.Keys);
    }

    [Fact]
    public async Task Archived_items_leave_the_public_menu_but_stay_in_the_admin_list()
    {
        var product = await api.CreateProductAsync([("Regular", 50m)]);
        var variantId = product.Variants[0].Id;

        var archived = await UpdateAsync(product, product.AsInput(isActive: false));
        Assert.False(archived.IsActive);

        Assert.Equal(HttpStatusCode.NotFound, (await Client.Call(HttpMethod.Get, $"/api/products/{product.Id}")).StatusCode);
        var menu = await (await Client.Call(HttpMethod.Get, "/api/products?category=Merchandise")).ReadAsync<List<ProductSummaryDto>>();
        Assert.DoesNotContain(menu, p => p.Id == product.Id);

        var adminList = await (await api.AdminAsync(HttpMethod.Get, "/api/admin/products")).ReadAsync<List<AdminProductDto>>();
        Assert.Contains(adminList, p => p.Id == product.Id && !p.IsActive);

        // The cart check reports it as unavailable
        var check = await (await Client.Call(HttpMethod.Post, "/api/products/check", new VariantCheckRequest([variantId])))
            .ReadAsync<List<VariantCheckDto>>();
        Assert.False(check.Single().Available);

        // Restoring brings it back
        await UpdateAsync(product, product.AsInput(isActive: true));
        Assert.Equal(HttpStatusCode.OK, (await Client.Call(HttpMethod.Get, $"/api/products/{product.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_cart_check_reports_availability_prices_and_deleted_items()
    {
        var product = await api.CreateProductAsync([("Small", 50m), ("Large", 80m)]);
        await UpdateAsync(product, Input(product.Name,
            new VariantInput(product.Variants[0].Id, "Small", 55m, true),
            new VariantInput(product.Variants[1].Id, "Large", 80m, false)));

        var request = new VariantCheckRequest([product.Variants[0].Id, product.Variants[1].Id, 999_999]);
        var check = await (await Client.Call(HttpMethod.Post, "/api/products/check", request)).ReadAsync<List<VariantCheckDto>>();

        Assert.Equal(new VariantCheckDto(product.Variants[0].Id, true, 55m), check[0]);
        Assert.Equal(new VariantCheckDto(product.Variants[1].Id, false, 80m), check[1]);   // sold out, but the price is still known
        Assert.Equal(new VariantCheckDto(999_999, false, null), check[2]);                 // no longer exists
    }

    [Fact]
    public async Task Items_with_orders_cannot_be_deleted_but_unused_ones_can()
    {
        var ordered = await api.CreateProductAsync([("Regular", 50m)]);
        await api.PlaceOrderAsync(api.BuildOrder(ordered.Variants[0].Id));
        var unused = await api.CreateProductAsync([("Regular", 50m), ("Large", 70m)]);

        var refused = await api.AdminAsync(HttpMethod.Delete, $"/api/admin/products/{ordered.Id}");
        var problem = await refused.ReadProblemAsync(HttpStatusCode.Conflict);
        Assert.Contains("past orders", problem.Title);

        await (await api.AdminAsync(HttpMethod.Delete, $"/api/admin/products/{unused.Id}")).ExpectAsync(HttpStatusCode.NoContent);

        Assert.Equal(HttpStatusCode.NotFound,
            (await api.AdminAsync(HttpMethod.Get, $"/api/admin/products/{unused.Id}")).StatusCode);
        var variantsLeft = await api.QueryDbAsync(db => db.ProductVariants.CountAsync(v => v.ProductId == unused.Id));
        Assert.Equal(0, variantsLeft);   // its options went with it
    }

    // ---------- Photos ----------

    private static byte[] Jpeg(int length = 200)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF;
        return bytes;
    }

    private async Task<HttpResponseMessage> UploadAsync(int productId, byte[] bytes, string? token = null)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(part, "file", "photo.jpg");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/products/{productId}/image") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? await api.AdminTokenAsync());
        return await Client.SendAsync(request);
    }

    [Fact]
    public async Task Photos_can_be_uploaded_replaced_and_removed_and_old_ones_are_cleaned_up()
    {
        var product = await api.CreateProductAsync([("Regular", 50m)]);

        var first = await (await UploadAsync(product.Id, Jpeg())).ReadAsync<AdminProductDto>();
        Assert.NotNull(first.ImageUrl);
        var firstId = await api.QueryDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.ImagePublicId).SingleAsync());

        var second = await (await UploadAsync(product.Id, Jpeg(300))).ReadAsync<AdminProductDto>();
        Assert.NotEqual(first.ImageUrl, second.ImageUrl);
        Assert.Contains(firstId!, api.Images.DeletedIds);               // the replaced photo was deleted

        var removed = await (await api.AdminAsync(HttpMethod.Delete, $"/api/admin/products/{product.Id}/image"))
            .ReadAsync<AdminProductDto>();
        Assert.Null(removed.ImageUrl);

        // Deleting an item also deletes its photo
        await UploadAsync(product.Id, Jpeg());
        var lastId = await api.QueryDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id).Select(p => p.ImagePublicId).SingleAsync());
        await api.AdminAsync(HttpMethod.Delete, $"/api/admin/products/{product.Id}");
        Assert.Contains(lastId!, api.Images.DeletedIds);
    }

    [Fact]
    public async Task Only_real_images_within_the_size_limit_are_accepted()
    {
        var product = await api.CreateProductAsync([("Regular", 50m)]);

        var notAnImage = await UploadAsync(product.Id, System.Text.Encoding.ASCII.GetBytes("definitely not a picture"));
        Assert.Contains("JPEG, PNG, or WebP", (await notAnImage.ReadProblemAsync(HttpStatusCode.BadRequest)).Title);

        var tooBig = await UploadAsync(product.Id, Jpeg(4 * 1024 * 1024 + 1));
        Assert.Contains("MB", (await tooBig.ReadProblemAsync(HttpStatusCode.BadRequest)).Title);

        var unknownProduct = await UploadAsync(999_999, Jpeg());
        Assert.Equal(HttpStatusCode.NotFound, unknownProduct.StatusCode);

        var (customerToken, _) = await api.RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(product.Id, Jpeg(), customerToken)).StatusCode);
    }

    [Fact]
    public async Task Uploads_report_a_clear_error_when_image_storage_is_not_configured()
    {
        var product = await api.CreateProductAsync([("Regular", 50m)]);

        api.Images.IsConfigured = false;
        try
        {
            var response = await UploadAsync(product.Id, Jpeg());
            var problem = await response.ReadProblemAsync(HttpStatusCode.ServiceUnavailable);
            Assert.Contains("aren't configured", problem.Title);
        }
        finally
        {
            api.Images.IsConfigured = true;
        }
    }
}

internal static class ProductTestExtensions
{
    /// <summary>The update request that would leave a product exactly as it is, with optional changes.</summary>
    public static ProductInput AsInput(this AdminProductDto product, bool? isActive = null) =>
        new(product.Name, product.Description, product.Category, isActive ?? product.IsActive,
            product.Variants.Select(v => new VariantInput(v.Id, v.Name, v.Price, v.IsAvailable)).ToList());
}