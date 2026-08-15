using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Events;
using Kart.Product.Domain.Variants;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Features.AddVariant;

public sealed class AddVariantCommandHandler(
    IProductGroupRepository productGroupRepository,
    IVariantRepository variantRepository,
    IOutboxEventWriter outboxEventWriter,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    TimeProvider timeProvider,
    ILogger<AddVariantCommandHandler> logger) : IRequestHandler<AddVariantCommand, AddVariantResponse>
{
    public async Task<AddVariantResponse> Handle(AddVariantCommand request, CancellationToken cancellationToken)
    {
        var productGroup = await productGroupRepository.GetByIdAsync(request.ProductGroupId, cancellationToken);
        if (productGroup is null)
        {
            logger.LogWarning("Stage {Stage}: add variant rejected, product-group {ProductGroupId} not found", "ProductGroupNotFound", request.ProductGroupId);
            throw new ProductGroupNotFoundException(request.ProductGroupId);
        }

        if (await variantRepository.ExistsAsync(request.Sku, cancellationToken))
        {
            logger.LogWarning("Stage {Stage}: add variant rejected, sku {Sku} already exists", "SkuAlreadyExists", request.Sku);
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
            now,
            productGroup.ImageUrl);

        outboxEventWriter.Enqueue(variant.Sku, domainEvent, clientId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: variant {Sku} added to product-group {ProductGroupId} and persisted, ProductCreated outbox event enqueued",
            "AddVariantProcessCompletedSuccessfully",
            variant.Sku,
            productGroup.Id);

        return new AddVariantResponse(variant.Sku);
    }
}
