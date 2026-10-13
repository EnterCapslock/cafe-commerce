using System.Globalization;
using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Cafe.Api.Tests.Unit.Validation;

public class ProductInputValidatorTests
{
    private readonly ProductInputValidator _validator = new();

    private static ProductInput Valid() => new(
        "Iced Latte", "Cold and sweet.", ProductCategory.Coffee, true,
        [new VariantInput(null, "12oz", 155m, true), new VariantInput(null, "16oz", 175m, true)]);

    [Fact]
    public void A_complete_product_is_valid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_product_needs_at_least_one_variant()
    {
        _validator.TestValidate(Valid() with { Variants = [] }).ShouldHaveValidationErrorFor(x => x.Variants);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void A_product_can_have_at_most_twenty_variants(int count, bool valid)
    {
        var variants = Enumerable.Range(1, count).Select(i => new VariantInput(null, $"V{i}", 10m, true)).ToList();
        var result = _validator.TestValidate(Valid() with { Variants = variants });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Variants);
        else result.ShouldHaveValidationErrorFor(x => x.Variants);
    }

    [Fact]
    public void Variant_names_must_be_unique_ignoring_case_and_spaces()
    {
        var request = Valid() with
        {
            Variants = [new VariantInput(null, "Large", 100m, true), new VariantInput(null, "  large ", 120m, true)]
        };

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Variants);
    }

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("120.5", true)]
    [InlineData("100000", true)]
    [InlineData("0", false)]
    [InlineData("-5", false)]
    [InlineData("100000.01", false)]
    [InlineData("1.234", false)]      // more than two decimal places
    public void Prices_must_be_positive_capped_and_in_whole_centavos(string price, bool valid)
    {
        var variants = new[] { new VariantInput(null, "Regular", decimal.Parse(price, CultureInfo.InvariantCulture), true) };
        var result = _validator.TestValidate(Valid() with { Variants = variants });

        if (valid) result.ShouldNotHaveValidationErrorFor("Variants[0].Price");
        else result.ShouldHaveValidationErrorFor("Variants[0].Price");
    }

    [Fact]
    public void A_variant_needs_a_name()
    {
        var variants = new[] { new VariantInput(null, "", 100m, true) };
        _validator.TestValidate(Valid() with { Variants = variants }).ShouldHaveValidationErrorFor("Variants[0].Name");
    }

    [Fact]
    public void Name_and_description_are_required_and_limited()
    {
        _validator.TestValidate(Valid() with { Name = "" }).ShouldHaveValidationErrorFor(x => x.Name);
        _validator.TestValidate(Valid() with { Name = new string('a', 121) }).ShouldHaveValidationErrorFor(x => x.Name);
        _validator.TestValidate(Valid() with { Description = "" }).ShouldHaveValidationErrorFor(x => x.Description);
        _validator.TestValidate(Valid() with { Description = new string('a', 1001) }).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void An_unknown_category_is_rejected()
    {
        _validator.TestValidate(Valid() with { Category = (ProductCategory)99 }).ShouldHaveValidationErrorFor(x => x.Category);
    }
}