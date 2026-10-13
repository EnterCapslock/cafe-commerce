using Cafe.Api.Models.Dtos;
using FluentValidation;
namespace Cafe.Api.Validators;

public class VariantCheckRequestValidator : AbstractValidator<VariantCheckRequest>
{
    public VariantCheckRequestValidator()
    {
        RuleFor(x => x.VariantIds)
            .NotEmpty()
            .Must(ids => ids.Count <= 50).WithMessage("Check at most 50 items at a time.");
        RuleForEach(x => x.VariantIds).GreaterThan(0);
    }
}