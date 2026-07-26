using Kart.Product.Domain.Variants;
using MediatR;

namespace Kart.Product.Application.Features.CreateProductGroup;

/// <summary>PRD-1 / api-contract.yaml <c>POST /v1/product-groups</c>. ddd-model.md's
/// Cross-Aggregate Interaction flow 1: saves ProductGroup(Draft) -> saves initial Variant ->
/// flips ProductGroup to Published -> publishes <c>ProductCreated</c>.</summary>
public sealed record CreateProductGroupCommand(
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    string Sku,
    Money Price,
    ProductAttributes Attributes) : IRequest<CreateProductGroupResponse>;
