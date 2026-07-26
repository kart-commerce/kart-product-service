using FluentValidation;

namespace Kart.Product.Application.Features.UpdateVariant;

public sealed class UpdateVariantCommandValidator : AbstractValidator<UpdateVariantCommand>
{
    public UpdateVariantCommandValidator()
    {
        RuleFor(c => c.Sku).NotEmpty();

        // "too few" (none of the three) is a structurally-incomplete request -> 400 (this
        // validator). "too many" (more than one) is checked in the handler, since api-contract.yaml
        // specifies 409 for that case specifically, not a generic validation failure.
        RuleFor(c => c)
            .Must(c => c.Price is not null || c.Status is not null || c.Attributes is not null)
            .WithMessage("Exactly one of price, status, or attributes must be provided.");

        RuleFor(c => c.Status)
            .Equal("Discontinued")
            .When(c => c.Status is not null)
            .WithMessage("status may only be set to 'Discontinued' - there is no un-discontinue path.");
    }
}
