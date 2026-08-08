using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using MediatR;

namespace Kart.Product.Application.Features.GetProduct;

public sealed class GetProductQueryHandler(IProductCache cache, IProductReadModelRepository readModelRepository)
    : IRequestHandler<GetProductQuery, ProductResponseDto>
{
    public async Task<ProductResponseDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Redis cache-aside in front of product_read_model (design-decisions.md, "Caching
        // Strategy for Product Reads") - a hit skips MongoDB entirely; a miss falls back to it
        // and repopulates the cache with a TTL.
        var readModel = await cache.GetAsync(request.Sku, cancellationToken);
        if (readModel is null)
        {
            readModel = await readModelRepository.GetBySkuAsync(request.Sku, cancellationToken)
                ?? throw new VariantNotFoundException(request.Sku);

            await cache.SetAsync(readModel, cancellationToken);
        }

        return new ProductResponseDto(
            readModel.Sku,
            readModel.Name,
            readModel.Description,
            new ProductResponseCategoryDto(readModel.Category.Id, readModel.Category.Name),
            readModel.Brand,
            new ProductResponseMoneyDto(readModel.PriceAmount, readModel.PriceCurrency),
            readModel.Status,
            new ProductResponseAttributesDto(readModel.Size, readModel.Color, readModel.ExtendedAttributes),
            new ProductResponseRatingSummaryDto(readModel.RatingSummary.Avg, readModel.RatingSummary.Count),
            readModel.LastUpdatedAt);
    }
}
