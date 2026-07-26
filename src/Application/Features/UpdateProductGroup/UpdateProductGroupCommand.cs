using MediatR;

namespace Kart.Product.Application.Features.UpdateProductGroup;

/// <summary>PRD-5 / api-contract.yaml <c>PATCH /v1/product-groups/{productGroupId}</c>. Either a
/// field edit (any of Name/Description/CategoryId/Brand) - fanning out one <c>ProductUpdated</c>
/// per currently-Active sibling Variant - or <c>Status = "Archived"</c>, cascading every Active
/// sibling to Discontinued. Mixing both in one call is rejected 409, by the same unambiguous-
/// outcome rule <c>UpdateVariant</c> applies to price/status/attributes.</summary>
public sealed record UpdateProductGroupCommand(
    Guid ProductGroupId,
    string? Name,
    string? Description,
    string? CategoryId,
    string? Brand,
    string? Status) : IRequest<UpdateProductGroupResponse>;
