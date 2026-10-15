using Cafe.Api.Models.Dtos;
using Cafe.Api.Services.Orders;
using FluentValidation;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(TimeProvider time, IOptions<OrderOptions> options)
    {
        var o = options.Value;

        RuleFor(x => x.PaymentMethod).IsInEnum();
        
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.CustomerEmail).NotEmpty().MaximumLength(254).EmailAddress();

        RuleFor(x => x.CustomerPhone)
            .NotEmpty()
            .Must(p => System.Text.RegularExpressions.Regex.IsMatch(
                System.Text.RegularExpressions.Regex.Replace(p, @"[\s\-]", ""),
                @"^(\+63|0)9\d{9}$"))
            .WithMessage("Enter a valid Philippine mobile number (e.g. 0917 123 4567).");

        RuleFor(x => x.PickupTime)
            .Must(t => t >= time.GetUtcNow().AddMinutes(o.MinLeadMinutes))
                .WithMessage($"Pickup time must be at least {o.MinLeadMinutes} minutes from now.")
            .Must(t => t <= time.GetUtcNow().AddDays(o.MaxAdvanceDays))
                .WithMessage($"Pickup time can be at most {o.MaxAdvanceDays} days ahead.");

        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Your order must contain at least one item.")
            .Must(items => items.Count <= o.MaxLines)
                .WithMessage($"An order can have at most {o.MaxLines} different items.")
            .Must(items => items.Select(i => i.VariantId).Distinct().Count() == items.Count)
                .WithMessage("Each item may appear only once. Combine quantities instead.");

        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(i => i.VariantId).GreaterThan(0);
            line.RuleFor(i => i.Quantity).InclusiveBetween(1, o.MaxQuantityPerLine);
        });
    }
}

public class LookupOrderRequestValidator : AbstractValidator<LookupOrderRequest>
{
    public LookupOrderRequestValidator()
    {
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter your order code.")
            .Must(code => OrderCode.Normalize(code) is not null)
                .WithMessage("The order code is 8 letters and numbers, like E18B681F.");

        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
    }
}