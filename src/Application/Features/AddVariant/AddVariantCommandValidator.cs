using FluentValidation;

namespace Kart.Product.Application.Features.AddVariant;

public sealed class AddVariantCommandValidator : AbstractValidator<AddVariantCommand>
{
    public AddVariantCommandValidator()
    {
        RuleFor(c => c.ProductGroupId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.Price.Amount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Price.Currency).NotEmpty().Length(3);
    }
}
