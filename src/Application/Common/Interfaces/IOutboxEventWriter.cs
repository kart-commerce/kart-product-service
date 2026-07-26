using Kart.Shared.Domain;

namespace Kart.Product.Application.Common.Interfaces;

/// <summary>
/// Adds a <c>product_outbox_events</c> row to the same <see cref="IUnitOfWork"/> transaction as
/// the domain write that produced it (the Transactional Outbox pattern) - the row is only durably
/// committed, and therefore only ever relayed to RabbitMQ, if the domain write itself committed.
/// The event's serialized type name (with a trailing "DomainEvent" suffix stripped, e.g.
/// <c>ProductCreatedDomainEvent</c> -&gt; <c>ProductCreated</c>) is looked up in
/// <c>message-bus-manifest.json</c>'s <c>publishedEvents</c> by <see cref="OutboxRelayHostedService"/> -
/// never hardcoded here.
/// </summary>
public interface IOutboxEventWriter
{
    void Enqueue(string sku, IDomainEvent domainEvent, string createdBy);
}
