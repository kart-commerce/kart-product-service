namespace Kart.Product.Api.Controllers.Requests;

/// <summary>api-contract.yaml <c>POST /v1/product-groups</c> request body.</summary>
public sealed record CreateProductGroupRequest(
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    string Sku,
    MoneyRequest Price,
    ProductAttributesRequest? Attributes,
    string? ImageUrl = null);
