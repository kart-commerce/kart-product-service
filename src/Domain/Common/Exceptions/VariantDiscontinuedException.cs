namespace Kart.Product.Domain.Common.Exceptions;

/// <summary>A discontinued Variant is terminal - its price/attributes may no longer be edited.</summary>
public sealed class VariantDiscontinuedException(string sku)
    : DomainInvariantException($"Variant '{sku}' is discontinued and can no longer be updated.")
{
    public string Sku { get; } = sku;
}
