using FluentValidation;

namespace Kart.Product.Application.Features.UpdateProductGroup;

public sealed class UpdateProductGroupCommandValidator : AbstractValidator<UpdateProductGroupCommand>
{
    public UpdateProductGroupCommandValidator()
    {
        RuleFor(c => c.ProductGroupId).NotEmpty();

        RuleFor(c => c)
            .Must(c => c.Name is not null || c.Description is not null || c.CategoryId is not null || c.Brand is not null || c.Status is not null)
            .WithMessage("At least one field to edit, or status: Archived, must be provided.");

        RuleFor(c => c.Status)
            .Equal("Archived")
            .When(c => c.Status is not null)
            .WithMessage("status may only be set to 'Archived' - there is no un-archive path.");
    }
}
