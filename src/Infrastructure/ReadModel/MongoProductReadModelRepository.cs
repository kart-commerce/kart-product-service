using System.Text.Json;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Kart.Product.Infrastructure.ReadModel;

/// <summary>
/// The MongoDB read side (database-design.md's <c>product_read_model</c>). Every write is a
/// field-scoped partial <c>$set</c> - each method touches only the fields its own projector owns,
/// plus <c>lastUpdatedAt</c>, so concurrent projections of different event types never clobber
/// each other (no version field/optimistic-lock retry loop needed).
/// </summary>
public sealed class MongoProductReadModelRepository(IMongoDatabase database) : IProductReadModelRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IMongoCollection<ProductReadModelDocument> Collection => database.GetCollection<ProductReadModelDocument>("product_read_model");

    public async Task<ProductReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var document = await Collection.Find(d => d.Id == sku).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToApplicationModel(document);
    }

    public async Task UpsertAsync(ProductReadModel readModel, CancellationToken cancellationToken)
    {
        var document = ToDocument(readModel);

        // An upsert against a sharded collection must include the full shard key (category.id,
        // database-design.md) in its filter, or mongos cannot determine which shard to route the
        // insert to ("Failed to target upsert by query :: could not extract exact shard key").
        // Filtering by _id alone works for the point-read/plain-update paths below, but not here.
        // Uses the literal field-path string ("category.id"), not the d => d.Category.Id lambda
        // form - the driver's expression translator was observed not resolving the nested
        // property's [BsonElement] mapping correctly for shard-key extraction purposes.
        var filter = Builders<ProductReadModelDocument>.Filter.And(
            Builders<ProductReadModelDocument>.Filter.Eq(d => d.Id, readModel.Sku),
            Builders<ProductReadModelDocument>.Filter.Eq("category.id", readModel.Category.Id));

        await Collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task UpdatePriceAsync(string sku, decimal amount, string currency, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<ProductReadModelDocument>.Update
            .Set(d => d.Price, new ProductReadModelPriceDocument { Amount = amount, Currency = currency })
            .Set(d => d.LastUpdatedAt, lastUpdatedAt.UtcDateTime);

        await Collection.UpdateOneAsync(d => d.Id == sku, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateFieldsAsync(string sku, IReadOnlyDictionary<string, object?> fields, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        var setDocument = new BsonDocument { ["lastUpdatedAt"] = lastUpdatedAt.UtcDateTime };

        foreach (var (path, value) in fields)
        {
            setDocument[path] = ToBsonValue(value);
        }

        var update = new BsonDocument("$set", setDocument);
        await Collection.UpdateOneAsync(Builders<ProductReadModelDocument>.Filter.Eq(d => d.Id, sku), update, cancellationToken: cancellationToken);
    }

    public async Task MarkDiscontinuedAsync(string sku, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<ProductReadModelDocument>.Update
            .Set(d => d.Status, "Discontinued")
            .Set(d => d.LastUpdatedAt, lastUpdatedAt.UtcDateTime);

        await Collection.UpdateOneAsync(d => d.Id == sku, update, cancellationToken: cancellationToken);
    }

    public async Task UpdateRatingSummaryAsync(string sku, double avg, int count, CancellationToken cancellationToken)
    {
        var update = Builders<ProductReadModelDocument>.Update
            .Set(d => d.RatingSummary, new ProductReadModelRatingSummaryDocument { Avg = avg, Count = count });

        await Collection.UpdateOneAsync(d => d.Id == sku, update, cancellationToken: cancellationToken);
    }

    private static BsonValue ToBsonValue(object? value) => value switch
    {
        null => BsonNull.Value,
        string s => new BsonString(s),
        IReadOnlyDictionary<string, object?> dict => BsonDocument.Parse(JsonSerializer.Serialize(dict, JsonOptions)),
        _ => BsonValue.Create(value),
    };

    private static ProductReadModelDocument ToDocument(ProductReadModel model) => new()
    {
        Id = model.Sku,
        ProductGroupId = model.ProductGroupId,
        Name = model.Name,
        Description = model.Description,
        Category = new ProductReadModelCategoryDocument { Id = model.Category.Id, Name = model.Category.Name },
        Brand = model.Brand,
        Price = new ProductReadModelPriceDocument { Amount = model.PriceAmount, Currency = model.PriceCurrency },
        Status = model.Status,
        Size = model.Size,
        Color = model.Color,
        ExtendedAttributes = BsonDocument.Parse(JsonSerializer.Serialize(model.ExtendedAttributes, JsonOptions)),
        RatingSummary = new ProductReadModelRatingSummaryDocument { Avg = model.RatingSummary.Avg, Count = model.RatingSummary.Count },
        LastUpdatedAt = model.LastUpdatedAt.UtcDateTime,
    };

    private static ProductReadModel ToApplicationModel(ProductReadModelDocument document) => new()
    {
        Sku = document.Id,
        ProductGroupId = document.ProductGroupId,
        Name = document.Name,
        Description = document.Description,
        Category = new ProductReadModelCategory(document.Category.Id, document.Category.Name),
        Brand = document.Brand,
        PriceAmount = document.Price.Amount,
        PriceCurrency = document.Price.Currency,
        Status = document.Status,
        Size = document.Size,
        Color = document.Color,
        ExtendedAttributes = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.ExtendedAttributes.ToJson(), JsonOptions) ?? new Dictionary<string, object?>(),
        RatingSummary = new ProductReadModelRatingSummary(document.RatingSummary.Avg, document.RatingSummary.Count),
        LastUpdatedAt = document.LastUpdatedAt,
    };
}
