using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Events;
using Kart.Product.Domain.Variants;
using MediatR;

namespace Kart.Product.Application.Features.AddVariant;

public sealed class AddVariantCommandHandler(
    IProductGroupRepository productGroupRepository,
    IVariantRepository variantRepository,
    IOutboxEventWriter outboxEventWriter,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    TimeProvider timeProvider) : IRequestHandler<AddVariantCommand, AddVariantResponse>
{
    public async Task<AddVariantResponse> Handle(AddVariantCommand request, CancellationToken cancellationToken)
    {
        var productGroup = await productGroupRepository.GetByIdAsync(request.ProductGroupId, cancellationToken)
            ?? throw new ProductGroupNotFoundException(request.ProductGroupId);

        if (await variantRepository.ExistsAsync(request.Sku, cancellationToken))
        {
            throw new SkuAlreadyExistsException(request.Sku);
        }

        var now = timeProvider.GetUtcNow();
        var clientId = currentPrincipal.ClientId;

        var variant = Variant.Create(request.Sku, productGroup.Id, request.Price, request.Attributes, clientId, now);
        variantRepository.Add(variant);

        var domainEvent = new ProductCreatedDomainEvent(
            variant.Sku,
            productGroup.Id,
            productGroup.Name,
            productGroup.Description,
            productGroup.CategoryId,
            productGroup.Brand,
            variant.Price,
            variant.Status.ToString(),
            variant.Attributes,
            now);

        outboxEventWriter.Enqueue(variant.Sku, domainEvent, clientId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddVariantResponse(variant.Sku);
    }
}
