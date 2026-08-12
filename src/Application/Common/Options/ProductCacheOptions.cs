namespace Kart.Product.Application.Common.Options;

/// <summary>
/// Tunables design-decisions.md's "Caching Strategy for Product Reads" fixes as an engineering
/// default - bound from the "ProductCache" configuration section rather than hardcoded in
/// <c>RedisProductCache</c>.
/// </summary>
public sealed class ProductCacheOptions
{
    /// <summary>
    /// Cache-aside TTL for a general product-detail read (design-decisions.md: "cache-aside with
    /// TTL for general product fields"). Price staleness beyond this window is separately bounded
    /// by the write-through path (<see cref="Kart.Product.Application.Common.Interfaces.IProductCache.UpdatePriceAsync"/>),
    /// not by this TTL - so this only needs to be short enough that a non-price catalog edit
    /// (name/description/attributes, or a discontinuation) settles within an acceptable eventual-
    /// consistency window, per architecture.md's platform-default "typically sub-second" CQRS
    /// window applied loosely here (a cache TTL is a coarser, cheaper bound than a projector).
    /// </summary>
    public int TtlSeconds { get; set; } = 60;
}
