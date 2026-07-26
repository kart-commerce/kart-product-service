namespace Kart.Product.Api.Controllers.Requests;

/// <summary>api-contract.yaml <c>PATCH /v1/product-groups/{productGroupId}</c> request body -
/// either a field edit, or <c>status: Archived</c>, never both (409 on mixed).</summary>
public sealed record UpdateProductGroupRequest(string? Name, string? Description, string? CategoryId, string? Brand, string? Status);
