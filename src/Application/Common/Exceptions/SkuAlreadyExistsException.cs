namespace Kart.Product.Application.Common.Exceptions;

/// <summary>edge-cases.md "SKU uniqueness enforcement" - a caller-supplied SKU that already
/// exists. Maps to 409 (api-contract.yaml).</summary>
public sealed class SkuAlreadyExistsException(string sku) : Exception($"SKU '{sku}' already exists.")
{
    public string Sku { get; } = sku;
}
