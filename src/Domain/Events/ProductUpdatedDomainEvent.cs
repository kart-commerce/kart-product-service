using Kart.Product.Domain.Variants;
using Kart.Shared.Domain;

namespace Kart.Product.Domain.Events;

/// <summary>event-contract.md: <c>product.product.updated</c>. Fired once per currently-Active
/// sibling Variant when a parent ProductGroup field edit (or a Variant's own attribute edit)
/// occurs - <see cref="ChangedFields"/> names only the fields the read-model projector should
/// field-scope its <c>$set</c> to (database-design.md's concurrency-control rule).</summary>
public sealed record ProductUpdatedDomainEvent(
    string Sku,
    IReadOnlyList<string> ChangedFields,
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    string Status,
    ProductAttributes Attributes,
    DateTimeOffset OccurredAt,
    string? ImageUrl = null) : IDomainEvent;
