using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using MediatR;

namespace Kart.Product.Application.Features.ListProductGroupVariants;

public sealed class ListProductGroupVariantsQueryHandler(IProductReadModelRepository readModelRepository)
    : IRequestHandler<ListProductGroupVariantsQuery, IReadOnlyList<ProductResponseDto>>
{
    public async Task<IReadOnlyList<ProductResponseDto>> Handle(ListProductGroupVariantsQuery request, CancellationToken cancellationToken)
    {
        var readModels = await readModelRepository.ListByProductGroupIdAsync(request.ProductGroupId, cancellationToken);

        return readModels
            .Select(readModel => new ProductResponseDto(
                readModel.Sku,
                readModel.Name,
                readModel.Description,
                new ProductResponseCategoryDto(readModel.Category.Id, readModel.Category.Name),
                readModel.Brand,
                new ProductResponseMoneyDto(readModel.PriceAmount, readModel.PriceCurrency),
                readModel.Status,
                new ProductResponseAttributesDto(readModel.Size, readModel.Color, readModel.ExtendedAttributes),
                new ProductResponseRatingSummaryDto(readModel.RatingSummary.Avg, readModel.RatingSummary.Count),
                readModel.LastUpdatedAt,
                readModel.ProductGroupId))
            .ToList();
    }
}
