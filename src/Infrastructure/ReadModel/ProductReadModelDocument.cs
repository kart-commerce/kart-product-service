using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Kart.Product.Infrastructure.ReadModel;

/// <summary>
/// database-design.md's <c>product_read_model</c> document - one per SKU (<c>_id: sku</c>),
/// sharded on <c>category.id</c>. Kept as its own BSON-attributed type (rather than reusing
/// Application's <c>ProductReadModel</c> POCO directly) so Application has no dependency on
/// MongoDB.Driver; <see cref="MongoProductReadModelRepository"/> maps between the two.
/// </summary>
public sealed class ProductReadModelDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("productGroupId")]
    public Guid ProductGroupId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    [BsonIgnoreIfNull]
    public string? Description { get; set; }

    [BsonElement("category")]
    public ProductReadModelCategoryDocument Category { get; set; } = new();

    [BsonElement("brand")]
    [BsonIgnoreIfNull]
    public string? Brand { get; set; }

    [BsonElement("price")]
    public ProductReadModelPriceDocument Price { get; set; } = new();

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("size")]
    [BsonIgnoreIfNull]
    public string? Size { get; set; }

    [BsonElement("color")]
    [BsonIgnoreIfNull]
    public string? Color { get; set; }

    [BsonElement("extendedAttributes")]
    public BsonDocument ExtendedAttributes { get; set; } = new();

    [BsonElement("ratingSummary")]
    public ProductReadModelRatingSummaryDocument RatingSummary { get; set; } = new();

    [BsonElement("lastUpdatedAt")]
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class ProductReadModelCategoryDocument
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; }
}

public sealed class ProductReadModelPriceDocument
{
    [BsonElement("amount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;
}

public sealed class ProductReadModelRatingSummaryDocument
{
    [BsonElement("avg")]
    public double Avg { get; set; }

    [BsonElement("count")]
    public int Count { get; set; }
}
