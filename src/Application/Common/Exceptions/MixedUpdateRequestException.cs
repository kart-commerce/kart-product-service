namespace Kart.Product.Application.Common.Exceptions;

/// <summary>
/// A request tried to mix two mutually-exclusive outcomes in one call - e.g.
/// <c>PATCH /v1/products/{sku}</c> supplying both <c>price</c> and <c>status</c> (api-contract.yaml:
/// "exactly one of these three outcomes fires per call"), or
/// <c>PATCH /v1/product-groups/{id}</c> supplying both a field edit and <c>status: Archived</c>
/// in the same request (applied here by the same unambiguous-outcome rule, for consistency).
/// Maps to 409.
/// </summary>
public sealed class MixedUpdateRequestException(string message) : Exception(message);
