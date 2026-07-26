using MediatR;

namespace Kart.Product.Application.Features.ProjectCatalogEvent;

/// <summary>
/// The write-side (PostgreSQL) to read-side (MongoDB <c>product_read_model</c>) sync mechanism:
/// dispatched by CatalogProjectionConsumerHostedService for each of this service's own four
/// published events (self-consumption via <c>product.catalog-projection.queue</c>), carrying the
/// raw JSON payload exactly as published - deserialized here, not at the transport layer, so the
/// projection logic stays testable independent of RabbitMQ.
/// </summary>
public sealed record ProjectCatalogEventCommand(string EventType, string PayloadJson) : IRequest;
