namespace Kart.Product.Domain.Variants;

/// <summary>
/// The Variant aggregate's own identity (ddd-model.md - Sku, not a surrogate Guid, is the key;
/// database-design.md explains why: every access path is a point lookup by <c>sku</c> and
/// nothing else). Protects the one invariant every one of those access paths silently assumed
/// but never enforced: never null/blank, and never two callers' "same" SKU disagreeing only by
/// incidental leading/trailing whitespace or length.
/// </summary>
public readonly record struct Sku
{
    private const int MaxLength = 64;

    public string Value { get; }

    private Sku(string value) => Value = value;

    /// <summary>Caller-supplied (Admin's catalog UI or the Partner bulk feed already assigns
    /// SKUs, api-contract.yaml) - this is the boundary that turns "any string" into "a SKU",
    /// trimming incidental whitespace so two requests differing only by that never collide-check
    /// as distinct.</summary>
    public static Sku From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"SKU '{trimmed}' exceeds the maximum length of {MaxLength} characters.", nameof(value));
        }

        return new Sku(trimmed);
    }

    public static implicit operator string(Sku sku) => sku.Value;

    public static implicit operator Sku(string value) => From(value);

    public override string ToString() => Value;
}
