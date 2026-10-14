using Cafe.Api.Models.Dtos;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Orders;
using Cafe.Api.Validators;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Cafe.Api.Tests.Unit.Validation;

public class CreateOrderRequestValidatorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 10, 4, 0, 0, TimeSpan.Zero));
    private readonly CreateOrderRequestValidator _validator;

    public CreateOrderRequestValidatorTests()
    {
        _validator = new CreateOrderRequestValidator(_time, Options.Create(new OrderOptions()));
    }

    private CreateOrderRequest Valid() => new(
        "Juan Dela Cruz",
        "juan@example.com",
        "09171234567",
        _time.GetUtcNow().AddHours(1),
        [new OrderLineRequest(1, 2)],
        PaymentMethod.PayAtCounter);

    [Fact]
    public void A_complete_request_is_valid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    // ---------- Pickup time ----------

    [Theory]
    [InlineData(-30, false)]
    [InlineData(14, false)]
    [InlineData(15, true)]            // exactly the minimum lead time is allowed
    [InlineData(60, true)]
    [InlineData(7 * 24 * 60, true)]   // exactly seven days ahead is allowed
    [InlineData(7 * 24 * 60 + 1, false)]
    public void Pickup_time_must_be_inside_the_booking_window(int minutesFromNow, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { PickupTime = _time.GetUtcNow().AddMinutes(minutesFromNow) });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.PickupTime);
        else result.ShouldHaveValidationErrorFor(x => x.PickupTime);
    }

    [Fact]
    public void Pickup_rules_follow_the_clock_not_a_fixed_date()
    {
        var request = Valid() with { PickupTime = _time.GetUtcNow().AddMinutes(20) };
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.PickupTime);

        _time.Advance(TimeSpan.FromMinutes(10)); // now the same pickup time is only 10 minutes away
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PickupTime);
    }

    // ---------- Phone ----------

    [Theory]
    [InlineData("09171234567")]
    [InlineData("0917 123 4567")]
    [InlineData("0917-123-4567")]
    [InlineData("+639171234567")]
    [InlineData("+63 917 123 4567")]
    public void Philippine_mobile_numbers_are_accepted_with_or_without_spaces(string phone)
    {
        _validator.TestValidate(Valid() with { CustomerPhone = phone }).ShouldNotHaveValidationErrorFor(x => x.CustomerPhone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("9171234567")]        // missing the leading 0
    [InlineData("0917123456")]        // too short
    [InlineData("091712345678")]      // too long
    [InlineData("0817 123 4567")]     // not a mobile prefix
    [InlineData("+1 555 123 4567")]
    [InlineData("abcdefghijk")]
    public void Other_phone_numbers_are_rejected(string phone)
    {
        _validator.TestValidate(Valid() with { CustomerPhone = phone }).ShouldHaveValidationErrorFor(x => x.CustomerPhone);
    }

    // ---------- Items ----------

    [Fact]
    public void An_empty_cart_is_rejected()
    {
        _validator.TestValidate(Valid() with { Items = [] }).ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void The_same_variant_twice_is_rejected()
    {
        var request = Valid() with { Items = [new OrderLineRequest(1, 1), new OrderLineRequest(1, 2)] };
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void An_order_can_have_at_most_twenty_lines(int lines, bool valid)
    {
        var items = Enumerable.Range(1, lines).Select(i => new OrderLineRequest(i, 1)).ToList();
        var result = _validator.TestValidate(Valid() with { Items = items });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Items);
        else result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void Quantity_must_be_between_one_and_twenty(int quantity, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Items = [new OrderLineRequest(1, quantity)] });

        if (valid) result.ShouldNotHaveValidationErrorFor("Items[0].Quantity");
        else result.ShouldHaveValidationErrorFor("Items[0].Quantity");
    }

    [Fact]
    public void A_variant_id_must_be_positive()
    {
        _validator.TestValidate(Valid() with { Items = [new OrderLineRequest(0, 1)] })
            .ShouldHaveValidationErrorFor("Items[0].VariantId");
    }

    // ---------- Contact details and payment ----------

    [Theory]
    [InlineData("", false)]
    [InlineData("Juan", true)]
    public void A_name_is_required(string name, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { CustomerName = name });

        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.CustomerName);
        else result.ShouldHaveValidationErrorFor(x => x.CustomerName);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void A_valid_email_is_required(string email)
    {
        _validator.TestValidate(Valid() with { CustomerEmail = email }).ShouldHaveValidationErrorFor(x => x.CustomerEmail);
    }

    [Fact]
    public void An_unknown_payment_method_is_rejected()
    {
        _validator.TestValidate(Valid() with { PaymentMethod = (PaymentMethod)99 })
            .ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }
}