namespace Kart.Product.Application.Common.Models;

/// <summary>
/// The denormalized <c>product_read_model</c> document shape (database-design.md), as seen by
/// Application code - deliberately a plain POCO with no MongoDB.Driver attributes, so Application
/// does not need a dependency on the Mongo driver just to describe this shape.
/// </summary>
public sealed class ProductReadModel
{
    public required string Sku { get; init; }

    public required Guid ProductGroupId { get; init; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required ProductReadModelCategory Category { get; set; }

    public string? Brand { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public required decimal PriceAmount { get; set; }

    public required string PriceCurrency { get; set; }

    public required string Status { get; set; }

    public string? Size { get; set; }

    public string? Color { get; set; }

    public IReadOnlyDictionary<string, object?> ExtendedAttributes { get; set; } = new Dictionary<string, object?>();

    public ProductReadModelRatingSummary RatingSummary { get; set; } = new(0, 0);

    public required DateTimeOffset LastUpdatedAt { get; set; }
}

public sealed record ProductReadModelCategory(string Id, string? Name);

public sealed record ProductReadModelRatingSummary(double Avg, int Count);
