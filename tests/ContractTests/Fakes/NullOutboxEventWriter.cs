using Kart.Product.Application.Common.Interfaces;
using Kart.Shared.Domain;

namespace Kart.Product.ContractTests.Fakes;

/// <summary>No real RabbitMQ/Postgres in the contract-test host - the outbox write is simply
/// discarded, since these tests assert HTTP wire-shape only.</summary>
public sealed class NullOutboxEventWriter : IOutboxEventWriter
{
    public void Enqueue(string sku, IDomainEvent domainEvent, string createdBy)
    {
    }
}
