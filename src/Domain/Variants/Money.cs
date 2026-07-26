namespace Kart.Product.Domain.Variants;

/// <summary>api-contract.yaml's <c>Money</c> schema - a Variant's base/list price (ddd-model.md:
/// price lives at the Variant/SKU level, never on the parent ProductGroup).</summary>
public sealed record Money(decimal Amount, string Currency);
