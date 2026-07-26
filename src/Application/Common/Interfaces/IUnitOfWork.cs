namespace Kart.Product.Application.Common.Interfaces;

/// <summary>The DbContext is the Unit of Work (database-standards.md) - a single
/// <see cref="SaveChangesAsync"/> commits the domain write and its Outbox row together, in one
/// PostgreSQL transaction.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
