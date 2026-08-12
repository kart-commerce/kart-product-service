using System.Text.Json;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Common.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Kart.Product.Infrastructure.Caching;

/// <summary>
/// design-decisions.md's "Caching Strategy for Product Reads": Redis cache-aside (general
/// fields, TTL) in front of <c>product_read_model</c>, plus a synchronous write-through path for
/// price fields only, mirroring kart-category-service's <c>RedisCategoryCache</c> and
/// kart-inventory-service's <c>RedisStockCache</c>. A miss (or a deserialization failure) returns
/// null so the caller falls back to <see cref="IProductReadModelRepository"/> - Redis
/// availability is a latency, not a correctness, dependency.
/// </summary>
public sealed class RedisProductCache(IConnectionMultiplexer connectionMultiplexer, IOptions<ProductCacheOptions> options) : IProductCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(options.Value.TtlSeconds);

    public async Task<ProductReadModel?> GetAsync(string sku, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var value = await database.StringGetAsync(Key(sku));
        return value.HasValue ? JsonSerializer.Deserialize<ProductReadModel>(value!, SerializerOptions) : null;
    }

    public async Task SetAsync(ProductReadModel value, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await database.StringSetAsync(Key(value.Sku), json, _ttl);
    }

    public async Task UpdatePriceAsync(string sku, decimal amount, string currency, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var key = Key(sku);

        var existing = await database.StringGetAsync(key);
        if (!existing.HasValue)
        {
            // Nothing cached yet for this SKU - the next cache-aside GetAsync miss repopulates it
            // straight from the read model, which will already carry the new price in the common
            // case. Nothing to write through into.
            return;
        }

        var cached = JsonSerializer.Deserialize<ProductReadModel>(existing!, SerializerOptions);
        if (cached is null || occurredAt <= cached.LastUpdatedAt)
        {
            // Stale relative to what is already cached (including an exact-timestamp redelivery
            // of the same event) - reject, mirroring the Mongo projector's own out-of-order
            // ProductPriceChanged guard (edge-cases.md) so this write-through path can't
            // re-introduce the staleness window it exists to close.
            return;
        }

        cached.PriceAmount = amount;
        cached.PriceCurrency = currency;
        cached.LastUpdatedAt = occurredAt;

        var json = JsonSerializer.Serialize(cached, SerializerOptions);
        await database.StringSetAsync(key, json, _ttl);
    }

    private static string Key(string sku) => $"product:{sku}";
}
