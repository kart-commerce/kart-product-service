using System.Collections.Concurrent;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.ProductGroups;

namespace Kart.Product.ContractTests.Fakes;

public sealed class InMemoryProductGroupRepository : IProductGroupRepository
{
    private readonly ConcurrentDictionary<Guid, ProductGroup> _store = new();

    public Task<ProductGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public void Add(ProductGroup productGroup) => _store[productGroup.Id] = productGroup;
}
