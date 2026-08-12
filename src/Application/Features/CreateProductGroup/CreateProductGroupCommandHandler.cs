using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Events;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Features.CreateProductGroup;

public sealed class CreateProductGroupCommandHandler(
    IProductGroupRepository productGroupRepository,
    IVariantRepository variantRepository,
    IOutboxEventWriter outboxEventWriter,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    TimeProvider timeProvider,
    ILogger<CreateProductGroupCommandHandler> logger) : IRequestHandler<CreateProductGroupCommand, CreateProductGroupResponse>
{
    public async Task<CreateProductGroupResponse> Handle(CreateProductGroupCommand request, CancellationToken cancellationToken)
    {
        if (await variantRepository.ExistsAsync(request.Sku, cancellationToken))
        {
            throw new SkuAlreadyExistsException(request.Sku);
        }

        var now = timeProvider.GetUtcNow();
        var clientId = currentPrincipal.ClientId;

        // ddd-model.md's Cross-Aggregate Interaction flow 1: two sequenced saves, never one
        // cross-aggregate transaction - both are added to the same DbContext here and committed
        // together only because they happen to share this one HTTP request, not because they are
        // one aggregate.
        var productGroup = ProductGroup.Create(request.Name, request.Description, request.CategoryId, request.Brand, clientId, now);
        productGroupRepository.Add(productGroup);

        var variant = Variant.Create(request.Sku, productGroup.Id, request.Price, request.Attributes, clientId, now);
        variantRepository.Add(variant);

        productGroup.Publish(clientId, now);

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

        logger.LogInformation(
            "Stage {Stage}: product-group {ProductGroupId} and variant {Sku} persisted to product_groups/variants",
            "ProductPersistedToDatabase",
            productGroup.Id,
            variant.Sku);
        logger.LogInformation(
            "Stage {Stage}: ProductCreated outbox event saved for sku {Sku}",
            "ProductOutboxEventSaved",
            variant.Sku);

        return new CreateProductGroupResponse(productGroup.Id, variant.Sku);
    }
}
