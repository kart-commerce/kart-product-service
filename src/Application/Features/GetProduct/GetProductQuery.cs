using Kart.Product.Application.Common.Models;
using MediatR;

namespace Kart.Product.Application.Features.GetProduct;

/// <summary>PRD-3 / api-contract.yaml <c>GET /v1/products/{sku}</c>. Public read path, served
/// directly from <c>product_read_model</c> - P95&lt;150ms/P99&lt;400ms (requirement-spec.md §3).</summary>
public sealed record GetProductQuery(string Sku) : IRequest<ProductResponseDto>;
