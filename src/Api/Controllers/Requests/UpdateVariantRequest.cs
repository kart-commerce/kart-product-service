namespace Kart.Product.Api.Controllers.Requests;

/// <summary>api-contract.yaml <c>PATCH /v1/products/{sku}</c> request body - exactly one of
/// price/status/attributes should be present (enforced by the command handler, 409 on mixed).</summary>
public sealed record UpdateVariantRequest(MoneyRequest? Price, string? Status, ProductAttributesRequest? Attributes);
