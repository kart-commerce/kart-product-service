using Kart.Product.Domain.Variants;
using MediatR;

namespace Kart.Product.Application.Features.AddVariant;

/// <summary>PRD-2 / api-contract.yaml <c>POST /v1/product-groups/{productGroupId}/variants</c>.
/// ddd-model.md's Cross-Aggregate Interaction flow 2 - no write to the parent ProductGroup,
/// which already exists and is already Published.</summary>
public sealed record AddVariantCommand(Guid ProductGroupId, string Sku, Money Price, ProductAttributes Attributes)
    : IRequest<AddVariantResponse>;
