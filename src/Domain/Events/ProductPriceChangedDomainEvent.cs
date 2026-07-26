using Kart.Product.Domain.Variants;
using Kart.Shared.Domain;

namespace Kart.Product.Domain.Events;

/// <summary>event-contract.md: <c>product.price.changed</c>. Carries <c>occurredAt</c> so
/// out-of-order delivery can be rejected consumer-side (edge-cases.md).</summary>
public sealed record ProductPriceChangedDomainEvent(
    string Sku,
    Money OldPrice,
    Money NewPrice,
    DateTimeOffset OccurredAt) : IDomainEvent;
