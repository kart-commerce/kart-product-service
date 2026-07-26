namespace Kart.Product.Application.Common.Models;

/// <summary>api-contract.yaml's <c>ProductResponse</c> schema - the one consistent response shape
/// returned by both the read path (<c>GetProduct</c>, built from the MongoDB read model) and the
/// write paths that echo back the current state (<c>UpdateVariant</c>, built from the freshly
/// updated write-side entities).</summary>
public sealed record ProductResponseDto(
    string Sku,
    string Name,
    string? Description,
    ProductResponseCategoryDto Category,
    string? Brand,
    ProductResponseMoneyDto Price,
    string Status,
    ProductResponseAttributesDto Attributes,
    ProductResponseRatingSummaryDto RatingSummary,
    DateTimeOffset LastUpdatedAt);

public sealed record ProductResponseCategoryDto(string Id, string? Name);

public sealed record ProductResponseMoneyDto(decimal Amount, string Currency);

public sealed record ProductResponseAttributesDto(string? Size, string? Color, IReadOnlyDictionary<string, object?> ExtendedAttributes);

public sealed record ProductResponseRatingSummaryDto(double Avg, int Count);
