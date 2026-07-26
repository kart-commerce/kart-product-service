using Kart.Product.Domain.Variants;

namespace Kart.Product.Api.Controllers.Requests;

/// <summary>api-contract.yaml's <c>ProductAttributes</c> schema, as received over the wire.</summary>
public sealed record ProductAttributesRequest(string? Size, string? Color, IReadOnlyDictionary<string, object?>? ExtendedAttributes)
{
    public ProductAttributes ToDomain() => new(Size, Color, ExtendedAttributes ?? new Dictionary<string, object?>());
}
