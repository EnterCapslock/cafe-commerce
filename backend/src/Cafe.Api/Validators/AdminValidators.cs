using Cafe.Api.Models.Dtos;
using FluentValidation;
namespace Cafe.Api.Validators;

public class VariantInputValidator : AbstractValidator<VariantInput>
{
    public VariantInputValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(50);
        RuleFor(v => v.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(100_000m)
            .PrecisionScale(10, 2, true).WithMessage("Price can have at most 2 decimal places.");
    }
}

public class ProductInputValidator : AbstractValidator<ProductInput>
{
    public ProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Category).IsInEnum();

        RuleFor(x => x.Variants)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("An item needs at least one variant.")
            .Must(v => v.Count <= 20).WithMessage("An item can have at most 20 variants.")
            .Must(v => v.Select(x => x.Name?.Trim().ToLowerInvariant() ?? "").Distinct().Count() == v.Count)
                .WithMessage("Variant names must be unique within an item.");

        RuleForEach(x => x.Variants).SetValidator(new VariantInputValidator());
    }
}

public class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}