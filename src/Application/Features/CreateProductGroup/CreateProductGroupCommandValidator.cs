using FluentValidation;

namespace Kart.Product.Application.Features.CreateProductGroup;

public sealed class CreateProductGroupCommandValidator : AbstractValidator<CreateProductGroupCommand>
{
    public CreateProductGroupCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.Sku).NotEmpty();
        RuleFor(c => c.Price.Amount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Price.Currency).NotEmpty().Length(3);
    }
}
