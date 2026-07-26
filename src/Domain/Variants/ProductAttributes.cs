namespace Kart.Product.Domain.Variants;

/// <summary>
/// ddd-model.md's hybrid EAV/JSONB value object: <see cref="Size"/>/<see cref="Color"/> are
/// first-class indexed columns (requirement-spec.md §2); everything else lives in the
/// schemaless <see cref="ExtendedAttributes"/> bag (variants.extended_attributes JSONB).
/// </summary>
public sealed record ProductAttributes(string? Size, string? Color, IReadOnlyDictionary<string, object?> ExtendedAttributes)
{
    public static ProductAttributes Empty { get; } = new(null, null, new Dictionary<string, object?>());
}
