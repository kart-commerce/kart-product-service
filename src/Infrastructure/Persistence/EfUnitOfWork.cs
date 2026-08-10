using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Variants;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kart.Product.Infrastructure.Persistence;

/// <summary>order-service's / wishlist-service's <c>EfUnitOfWork</c> precedent - Postgres-specific
/// exception translation kept out of the Application layer, so handlers never need a local
/// try/catch (LoggingBehaviour's own remarks: exceptions are caught exactly once, by the global
/// exception handler).</summary>
public sealed class EfUnitOfWork(ProductDbContext dbContext) : IUnitOfWork
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationSqlState, ConstraintName: "PK_variants" })
        {
            // edge-cases.md "SKU uniqueness enforcement": the app-level ExistsAsync pre-check in
            // CreateProductGroupCommandHandler/AddVariantCommandHandler is a TOCTOU race, not the
            // actual guarantee - two concurrent requests for the same caller-supplied sku can both
            // pass that check. This constraint (variants' own primary key, per VariantConfiguration)
            // is the real backstop; translating its violation here means a raced-out second writer
            // still gets the contract's documented 409 SKU_ALREADY_EXISTS instead of an unhandled
            // 500, without any Application-layer handler needing its own try/catch.
            var sku = dbContext.ChangeTracker.Entries<Variant>()
                .FirstOrDefault(e => e.State == EntityState.Added)
                ?.Entity.Sku;

            throw new SkuAlreadyExistsException(sku ?? "unknown");
        }
    }
}
