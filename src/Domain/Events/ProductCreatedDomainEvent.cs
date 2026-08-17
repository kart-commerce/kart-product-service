using Kart.Product.Domain.Variants;
using Kart.Shared.Domain;

namespace Kart.Product.Domain.Events;

/// <summary>event-contract.md: published on <c>product.exchange</c> / <c>product.product.created</c>,
/// consumed by Search/Recommendation/Analytics (and self-consumed for the read-model projection).
/// <see cref="ProductGroupId"/> is an additive field beyond event-contract.md's documented
/// payload (sku/name/description/categoryId/brand/price/status/attributes) - needed so the
/// self-consumption projector can populate <c>product_read_model.productGroupId</c> (database-design.md)
/// without a second lookup. Additive fields are non-breaking (consumers ignore unknown fields,
/// kart-conventions.md's API Versioning policy applied identically to events per edge-cases.md's
/// additive-only schema evolution decision).</summary>
public sealed record ProductCreatedDomainEvent(
    string Sku,
    Guid ProductGroupId,
    string Name,
    string? Description,
    string CategoryId,
    string? Brand,
    Money Price,
    string Status,
    ProductAttributes Attributes,
    DateTimeOffset OccurredAt,
    string? ImageUrl = null) : IDomainEvent;
