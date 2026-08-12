using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Events;
using Kart.Product.Domain.Variants;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Features.UpdateProductGroup;

public sealed class UpdateProductGroupCommandHandler(
    IProductGroupRepository productGroupRepository,
    IVariantRepository variantRepository,
    IOutboxEventWriter outboxEventWriter,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    TimeProvider timeProvider,
    ILogger<UpdateProductGroupCommandHandler> logger) : IRequestHandler<UpdateProductGroupCommand, UpdateProductGroupResponse>
{
    public async Task<UpdateProductGroupResponse> Handle(UpdateProductGroupCommand request, CancellationToken cancellationToken)
    {
        var hasFieldEdit = request.Name is not null || request.Description is not null || request.CategoryId is not null || request.Brand is not null;
        var hasArchive = request.Status is not null;

        if (hasFieldEdit && hasArchive)
        {
            throw new MixedUpdateRequestException("A field edit may not be combined with status: Archived in the same request.");
        }

        var productGroup = await productGroupRepository.GetByIdAsync(request.ProductGroupId, cancellationToken)
            ?? throw new ProductGroupNotFoundException(request.ProductGroupId);

        var now = timeProvider.GetUtcNow();
        var clientId = currentPrincipal.ClientId;
        var affectedSkus = new List<string>();

        if (hasArchive)
        {
            // ddd-model.md's Cross-Aggregate Interaction flow 3 (archive path): a sequenced,
            // per-SKU operation, never a bulk/atomic multi-document write.
            productGroup.Archive(clientId, now);

            var siblings = await variantRepository.GetByProductGroupIdAsync(productGroup.Id, cancellationToken);
            foreach (var sibling in siblings.Where(v => v.Status == VariantStatus.Active))
            {
                sibling.Discontinue(clientId, now);
                outboxEventWriter.Enqueue(sibling.Sku, new ProductDiscontinuedDomainEvent(sibling.Sku, now), clientId);
                affectedSkus.Add(sibling.Sku);
            }
        }
        else
        {
            var changedFields = productGroup.UpdateFields(request.Name, request.Description, request.CategoryId, request.Brand, clientId, now);

            if (changedFields.Count > 0)
            {
                var siblings = await variantRepository.GetByProductGroupIdAsync(productGroup.Id, cancellationToken);
                foreach (var sibling in siblings.Where(v => v.Status == VariantStatus.Active))
                {
                    var domainEvent = new ProductUpdatedDomainEvent(
                        sibling.Sku,
                        changedFields,
                        productGroup.Name,
                        productGroup.Description,
                        productGroup.CategoryId,
                        productGroup.Brand,
                        sibling.Status.ToString(),
                        sibling.Attributes,
                        now);

                    outboxEventWriter.Enqueue(sibling.Sku, domainEvent, clientId);
                    affectedSkus.Add(sibling.Sku);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: product-group {ProductGroupId} persisted ({Operation}), {AffectedSkuCount} sibling variant(s) affected",
            "ProductPersistedToDatabase",
            productGroup.Id,
            hasArchive ? "archive" : "field-edit",
            affectedSkus.Count);

        if (affectedSkus.Count > 0)
        {
            logger.LogInformation(
                "Stage {Stage}: {EventType} outbox event(s) saved for sku(s) {Skus}",
                "ProductOutboxEventSaved",
                hasArchive ? "ProductDiscontinued" : "ProductUpdated",
                affectedSkus);
        }

        return new UpdateProductGroupResponse(productGroup.Id, affectedSkus);
    }
}
