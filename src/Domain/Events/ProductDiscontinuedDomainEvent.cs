using Kart.Shared.Domain;

namespace Kart.Product.Domain.Events;

/// <summary>event-contract.md: <c>product.product.discontinued</c>.</summary>
public sealed record ProductDiscontinuedDomainEvent(string Sku, DateTimeOffset OccurredAt) : IDomainEvent;
