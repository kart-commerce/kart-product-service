using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using MediatR;

namespace Kart.Product.Application.Features.GetProduct;

public sealed class GetProductQueryHandler(IProductReadModelRepository readModelRepository)
    : IRequestHandler<GetProductQuery, ProductResponseDto>
{
    public async Task<ProductResponseDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var readModel = await readModelRepository.GetBySkuAsync(request.Sku, cancellationToken)
            ?? throw new VariantNotFoundException(request.Sku);

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
