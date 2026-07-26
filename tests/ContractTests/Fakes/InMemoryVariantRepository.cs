using System.Collections.Concurrent;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Variants;

namespace Kart.Product.ContractTests.Fakes;

public sealed class InMemoryVariantRepository : IVariantRepository
{
    private readonly ConcurrentDictionary<string, Variant> _store = new();

    public Task<Variant?> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault(sku));

    public Task<bool> ExistsAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_store.ContainsKey(sku));

    public Task<IReadOnlyList<Variant>> GetByProductGroupIdAsync(Guid productGroupId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Variant>>(_store.Values.Where(v => v.ProductGroupId == productGroupId).ToList());

    public void Add(Variant variant) => _store[variant.Sku] = variant;
}
