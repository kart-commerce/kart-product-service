using System.Collections.Concurrent;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;

namespace Kart.Product.ContractTests.Fakes;

public sealed class InMemoryProductReadModelRepository : IProductReadModelRepository
{
    private readonly ConcurrentDictionary<string, ProductReadModel> _store = new();

    public Task<ProductReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault(sku));

    public Task UpsertAsync(ProductReadModel readModel, CancellationToken cancellationToken)
    {
        _store[readModel.Sku] = readModel;
        return Task.CompletedTask;
    }

    public Task UpdatePriceAsync(string sku, decimal amount, string currency, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(sku, out var existing))
        {
            existing.PriceAmount = amount;
            existing.PriceCurrency = currency;
            existing.LastUpdatedAt = lastUpdatedAt;
        }

        return Task.CompletedTask;
    }

    public Task UpdateFieldsAsync(string sku, IReadOnlyDictionary<string, object?> fields, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(sku, out var existing))
        {
            existing.LastUpdatedAt = lastUpdatedAt;
        }

        return Task.CompletedTask;
    }

    public Task MarkDiscontinuedAsync(string sku, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(sku, out var existing))
        {
            existing.Status = "Discontinued";
            existing.LastUpdatedAt = lastUpdatedAt;
        }

        return Task.CompletedTask;
    }

    public Task UpdateRatingSummaryAsync(string sku, double avg, int count, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(sku, out var existing))
        {
            existing.RatingSummary = new ProductReadModelRatingSummary(avg, count);
        }

        return Task.CompletedTask;
    }

    public void Seed(ProductReadModel readModel) => _store[readModel.Sku] = readModel;
}
