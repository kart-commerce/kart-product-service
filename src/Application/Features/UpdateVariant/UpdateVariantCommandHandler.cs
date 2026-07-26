using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Domain.Events;
using Kart.Shared.Domain;
using MediatR;

namespace Kart.Product.Application.Features.UpdateVariant;

public sealed class UpdateVariantCommandHandler(
    IVariantRepository variantRepository,
    IProductGroupRepository productGroupRepository,
    IProductReadModelRepository readModelRepository,
    IOutboxEventWriter outboxEventWriter,
    IUnitOfWork unitOfWork,
    ICurrentPrincipal currentPrincipal,
    TimeProvider timeProvider) : IRequestHandler<UpdateVariantCommand, ProductResponseDto>
{
    public async Task<ProductResponseDto> Handle(UpdateVariantCommand request, CancellationToken cancellationToken)
    {
        var providedCount = new[] { request.Price is not null, request.Status is not null, request.Attributes is not null }.Count(x => x);
        if (providedCount > 1)
        {
            throw new MixedUpdateRequestException("Exactly one of price, status, or attributes may be provided per call.");
        }

        var variant = await variantRepository.GetBySkuAsync(request.Sku, cancellationToken)
            ?? throw new VariantNotFoundException(request.Sku);

        var productGroup = await productGroupRepository.GetByIdAsync(variant.ProductGroupId, cancellationToken)
            ?? throw new ProductGroupNotFoundException(variant.ProductGroupId);

        var now = timeProvider.GetUtcNow();
        var clientId = currentPrincipal.ClientId;

        IDomainEvent domainEvent;

        if (request.Price is not null)
        {
            var oldPrice = variant.ChangePrice(request.Price, clientId, now);
            domainEvent = new ProductPriceChangedDomainEvent(variant.Sku, oldPrice, request.Price, now);
        }
        else if (request.Status is not null)
        {
            variant.Discontinue(clientId, now);
            domainEvent = new ProductDiscontinuedDomainEvent(variant.Sku, now);
        }
        else
        {
            variant.UpdateAttributes(request.Attributes!, clientId, now);
            domainEvent = new ProductUpdatedDomainEvent(
                variant.Sku,
                ["size", "color", "extendedAttributes"],
                productGroup.Name,
                productGroup.Description,
                productGroup.CategoryId,
                productGroup.Brand,
                variant.Status.ToString(),
                variant.Attributes,
                now);
        }

        outboxEventWriter.Enqueue(variant.Sku, domainEvent, clientId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Best-effort read of the existing projected ratingSummary (owned entirely by the
        // ReviewSubmitted/ReviewUpdated projector, PRD-6) so the response is complete - the write
        // path itself never touches this field.
        var existingReadModel = await readModelRepository.GetBySkuAsync(variant.Sku, cancellationToken);
        var ratingSummary = existingReadModel is null
            ? new ProductResponseRatingSummaryDto(0, 0)
            : new ProductResponseRatingSummaryDto(existingReadModel.RatingSummary.Avg, existingReadModel.RatingSummary.Count);

        return new ProductResponseDto(
            variant.Sku,
            productGroup.Name,
            productGroup.Description,
            new ProductResponseCategoryDto(productGroup.CategoryId, null),
            productGroup.Brand,
            new ProductResponseMoneyDto(variant.Price.Amount, variant.Price.Currency),
            variant.Status.ToString(),
            new ProductResponseAttributesDto(variant.Size, variant.Color, variant.ExtendedAttributes),
            ratingSummary,
            now);
    }
}
