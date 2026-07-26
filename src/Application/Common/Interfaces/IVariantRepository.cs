using Kart.Product.Domain.Variants;

namespace Kart.Product.Application.Common.Interfaces;

/// <summary>One repository per Aggregate Root only (coding-standards.md).</summary>
public interface IVariantRepository
{
    Task<Variant?> GetBySkuAsync(string sku, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string sku, CancellationToken cancellationToken);

    /// <summary>Supports the archive cascade (ddd-model.md flow 3: "every currently-Active
    /// sibling Variant") and the parent-edit fan-out.</summary>
    Task<IReadOnlyList<Variant>> GetByProductGroupIdAsync(Guid productGroupId, CancellationToken cancellationToken);

    void Add(Variant variant);
}
