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
            logger.LogWarning("Stage {Stage}: product-group creation rejected, sku {Sku} already exists", "SkuAlreadyExists", request.Sku);
            throw new SkuAlreadyExistsException(request.Sku);
        }

        var now = timeProvider.GetUtcNow();
        var clientId = currentPrincipal.ClientId;

        // ddd-model.md's Cross-Aggregate Interaction flow 1: two sequenced saves, never one
        // cross-aggregate transaction - both are added to the same DbContext here and committed
        // together only because they happen to share this one HTTP request, not because they are
        // one aggregate.
        var productGroup = ProductGroup.Create(request.Name, request.Description, request.CategoryId, request.Brand, clientId, now, request.ImageUrl);
        productGroupRepository.Add(productGroup);

        var variant = Variant.Create(request.Sku, productGroup.Id, request.Price, request.Attributes, clientId, now);
        variantRepository.Add(variant);

        // ddd-model.md: unlike the BRD's illustrative Admin flow diagram (Submit for Approval ->
        // Product Review (QC Team) -> Approve/Reject), this service's approved model has no
        // separate manual QC gate to branch on - every product-group is auto-published
        // (Draft -> Published) the moment its initial Variant is saved. Logged as its own
        // decision-worthy transition anyway, since "Product Published" is a distinct, searchable
        // BRD step even though this service's own code path to it is unconditional.
        productGroup.Publish(clientId, now);
        logger.LogInformation(
            "Stage {Stage}: product-group {ProductGroupId} auto-published (no manual QC/approval gate in this service's model)",
            "ProductAutoPublishedNoApprovalGate",
            productGroup.Id);

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
            "Stage {Stage}: product-group {ProductGroupId} and variant {Sku} persisted to product_groups/variants",
            "ProductPersistedToDatabase",
            productGroup.Id,
            variant.Sku);
        logger.LogInformation(
            "Stage {Stage}: ProductCreated outbox event saved for sku {Sku}",
            "ProductOutboxEventSaved",
            variant.Sku);
        logger.LogInformation(
            "Stage {Stage}: product-group {ProductGroupId} / sku {Sku} creation completed",
            "CreateProductGroupProcessCompletedSuccessfully",
            productGroup.Id,
            variant.Sku);

        return new CreateProductGroupResponse(productGroup.Id, variant.Sku);
    }
}
