using Kart.Product.Application.Common.Models;
using MediatR;

namespace Kart.Product.Application.Features.ListProductGroupVariants;

/// <summary>Public read — every sibling SKU (variant) sharing a product group, for a PDP's variant-axis picker.</summary>
public sealed record ListProductGroupVariantsQuery(Guid ProductGroupId) : IRequest<IReadOnlyList<ProductResponseDto>>;
