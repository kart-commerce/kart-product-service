using Kart.Product.Domain.ProductGroups;

namespace Kart.Product.Application.Common.Interfaces;

/// <summary>One repository per Aggregate Root only (coding-standards.md) - never a generic
/// per-entity/per-table repository.</summary>
public interface IProductGroupRepository
{
    Task<ProductGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(ProductGroup productGroup);
}
