using System.Diagnostics;
using System.Text.Json;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Outbox;
using Kart.Product.Infrastructure.Persistence;
using Kart.Shared.Domain;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>Adds the Outbox row to the same <see cref="ProductDbContext"/> change tracker as the
/// domain write it accompanies - both are committed together by the one
/// <see cref="IUnitOfWork.SaveChangesAsync"/> call the handler makes afterwards.</summary>
public sealed class OutboxEventWriter(ProductDbContext dbContext) : IOutboxEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue(string sku, IDomainEvent domainEvent, string createdBy)
    {
        var eventType = EventTypeName(domainEvent);
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);

        // Activity.Current here is still the inbound HTTP request's own Activity (ASP.NET Core's
        // auto-instrumentation started it, and Enqueue always runs synchronously within that
        // request) - captured now because by the time OutboxRelayHostedService's background
        // poller picks this row up, seconds later, on its own unrelated async context,
        // Activity.Current there has nothing to do with this request anymore.
        var traceParent = Activity.Current?.Id;

        var outboxEvent = ProductOutboxEvent.Create(eventType, sku, payload, domainEvent.OccurredAt, createdBy, traceParent);
        dbContext.OutboxEvents.Add(outboxEvent);
    }

    /// <summary>Strips the "DomainEvent" suffix (e.g. <c>ProductCreatedDomainEvent</c> -&gt;
    /// <c>ProductCreated</c>) so the runtime type name matches the event names
    /// <c>message-bus-manifest.json</c>'s <c>publishedEvents</c> and event-contract.md both use,
    /// without hand-maintaining a second mapping table.</summary>
    private static string EventTypeName(IDomainEvent domainEvent)
    {
        const string suffix = "DomainEvent";
        var typeName = domainEvent.GetType().Name;
        return typeName.EndsWith(suffix, StringComparison.Ordinal) ? typeName[..^suffix.Length] : typeName;
    }
}
