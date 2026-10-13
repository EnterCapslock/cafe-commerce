using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services;
using Xunit;

namespace Cafe.Api.Tests.Unit.Mapping;

public class ProductMappingTests
{
    private static Product Product(params (string Name, decimal Price, bool Available, int Sort)[] variants) => new()
    {
        Id = 1,
        Name = "Latte",
        Description = "Smooth.",
        Category = ProductCategory.Coffee,
        Variants = variants
            .Select((v, i) => new ProductVariant { Id = i + 1, Name = v.Name, Price = v.Price, IsAvailable = v.Available, SortOrder = v.Sort })
            .ToList()
    };

    [Fact]
    public void The_from_price_is_the_cheapest_available_variant()
    {
        var product = Product(("Small", 100m, false, 0), ("Medium", 120m, true, 1), ("Large", 150m, true, 2));

        var summary = product.ToSummaryDto();

        Assert.Equal(120m, summary.FromPrice);   // the sold-out ₱100 option doesn't count
        Assert.True(summary.IsAvailable);
        Assert.Equal(3, summary.VariantCount);
    }

    [Fact]
    public void When_everything_is_sold_out_the_from_price_is_the_cheapest_overall()
    {
        var product = Product(("Small", 100m, false, 0), ("Large", 150m, false, 1));

        var summary = product.ToSummaryDto();

        Assert.Equal(100m, summary.FromPrice);
        Assert.False(summary.IsAvailable);
    }

    [Fact]
    public void An_item_with_one_variant_uses_its_price()
    {
        Assert.Equal(95m, Product(("Regular", 95m, true, 0)).ToSummaryDto().FromPrice);
    }

    [Fact]
    public void Detail_variants_follow_the_sort_order_not_the_id_order()
    {
        var product = Product(("Large", 150m, true, 2), ("Small", 100m, true, 0), ("Medium", 120m, true, 1));

        var detail = product.ToDetailDto();

        Assert.Equal(["Small", "Medium", "Large"], detail.Variants.Select(v => v.Name));
    }

    [Fact]
    public void The_admin_view_includes_inactive_state_and_sort_order()
    {
        var product = Product(("Large", 150m, true, 1), ("Small", 100m, false, 0));
        product.IsActive = false;

        var admin = product.ToAdminDto();

        Assert.False(admin.IsActive);
        Assert.Equal(["Small", "Large"], admin.Variants.Select(v => v.Name));
        Assert.False(admin.Variants[0].IsAvailable);
    }
}