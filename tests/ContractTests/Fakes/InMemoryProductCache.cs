using System.Collections.Concurrent;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;

namespace Kart.Product.ContractTests.Fakes;

/// <summary>Always a miss unless explicitly seeded/populated by SetAsync - lets ContractTests
/// assert on the HTTP wire shape without needing a real Redis, mirroring the other InMemory*
/// fakes in this folder.</summary>
public sealed class InMemoryProductCache : IProductCache
{
    private readonly ConcurrentDictionary<string, ProductReadModel> _store = new();

    public Task<ProductReadModel?> GetAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault(sku));

    public Task SetAsync(ProductReadModel value, CancellationToken cancellationToken)
    {
        _store[value.Sku] = value;
        return Task.CompletedTask;
    }

    public Task UpdatePriceAsync(string sku, decimal amount, string currency, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(sku, out var existing) && occurredAt > existing.LastUpdatedAt)
        {
            existing.PriceAmount = amount;
            existing.PriceCurrency = currency;
            existing.LastUpdatedAt = occurredAt;
        }

        return Task.CompletedTask;
    }
}
