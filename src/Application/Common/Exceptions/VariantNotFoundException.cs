namespace Kart.Product.Application.Common.Exceptions;

/// <summary>No Variant exists for this SKU. Maps to 404 (api-contract.yaml).</summary>
public sealed class VariantNotFoundException(string sku) : Exception($"Variant '{sku}' was not found.")
{
    public string Sku { get; } = sku;
}
