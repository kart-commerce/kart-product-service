namespace Kart.Product.Api.Controllers.Requests;

/// <summary>api-contract.yaml <c>POST /v1/product-groups/{productGroupId}/variants</c> request body.</summary>
public sealed record AddVariantRequest(string Sku, MoneyRequest Price, ProductAttributesRequest? Attributes);
