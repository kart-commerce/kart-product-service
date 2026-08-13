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

    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

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
    // Deliberately NOT named "Id" - the MongoDB C# driver's default auto-mapping conventions
    // treat any member literally named "Id"/"id"/"_id" as this class's own BSON identifier and
    // silently force its serialized element name to "_id", overriding the explicit
    // [BsonElement("id")] attribute below (this convention exists for document ROOTS, which need
    // an `_id`; it doesn't know this is a nested/embedded sub-document with no such need). Left
    // unnoticed, every real document was actually stored/read as `category._id`, not the
    // documented `category.id` (database-design.md, BRD §6.2's own worked example) - every
    // existing test only round-tripped through this same serializer without ever inspecting the
    // raw stored JSON, so it never surfaced. Renaming the C# member (while keeping the explicit
    // attribute) sidesteps the convention, since it keys off the member name, not the
    // [BsonElement] value.
    [BsonElement("id")]
    public string CategoryId { get; set; } = string.Empty;

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
