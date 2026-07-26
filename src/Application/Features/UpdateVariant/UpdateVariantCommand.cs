using Kart.Product.Application.Common.Models;
using Kart.Product.Domain.Variants;
using MediatR;

namespace Kart.Product.Application.Features.UpdateVariant;

/// <summary>PRD-4 / api-contract.yaml <c>PATCH /v1/products/{sku}</c>. Exactly one of
/// <see cref="Price"/>/<see cref="Status"/>/<see cref="Attributes"/> may be set - a request
/// supplying more than one is rejected 409 (MixedUpdateRequestException).</summary>
public sealed record UpdateVariantCommand(string Sku, Money? Price, string? Status, ProductAttributes? Attributes)
    : IRequest<ProductResponseDto>;
