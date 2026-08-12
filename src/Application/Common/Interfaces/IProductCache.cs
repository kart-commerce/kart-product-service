using Kart.Product.Application.Common.Models;

namespace Kart.Product.Application.Common.Interfaces;

/// <summary>
/// Redis cache-aside layer in front of <see cref="IProductReadModelRepository"/> for
/// GET /v1/products/{sku} (design-decisions.md, "Caching Strategy for Product Reads" -
/// requirement-spec §3's P95 &lt; 150ms/P99 &lt; 400ms read budget). General fields are populated
/// on a cache miss with a TTL; price is additionally kept synchronously in step with the
/// PostgreSQL write path (write-through), so a warm cache entry never serves a stale price during
/// the window between a <c>ProductPriceChanged</c> commit and the eventual Outbox -&gt; RabbitMQ -&gt;
/// Mongo projection catching up (edge-cases.md, "Read-model staleness after a price change,
/// compounded by the Redis cache"). A miss always falls back to
/// <see cref="IProductReadModelRepository"/>; Redis availability is a latency, not a correctness,
/// dependency - the same posture kart-category-service's <c>ICategoryCache</c> and
/// kart-inventory-service's <c>IStockCache</c> already take.
/// </summary>
public interface IProductCache
{
    Task<ProductReadModel?> GetAsync(string sku, CancellationToken cancellationToken);

    Task SetAsync(ProductReadModel value, CancellationToken cancellationToken);

    /// <summary>
    /// Write-through: updates only the price fields of an already-cached entry, synchronously
    /// with the Postgres write that changed it (design-decisions.md's chosen option, over plain
    /// TTL-only cache-aside for price). Rejects an <paramref name="occurredAt"/> older than or
    /// equal to what is already cached - the same out-of-order-delivery guard edge-cases.md
    /// requires for the Mongo projection, applied here too since a stale write-through would
    /// otherwise re-introduce the exact staleness window this method exists to close. No-ops if
    /// nothing is cached yet for this SKU - the next cache-aside read repopulates it from the read
    /// model, which will already reflect the new price by then in the common case.
    /// </summary>
    Task UpdatePriceAsync(string sku, decimal amount, string currency, DateTimeOffset occurredAt, CancellationToken cancellationToken);
}
