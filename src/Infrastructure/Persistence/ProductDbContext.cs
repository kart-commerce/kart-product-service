using Kart.Product.Domain.Outbox;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Microsoft.EntityFrameworkCore;

namespace Kart.Product.Infrastructure.Persistence;

/// <summary>The write-side Unit of Work (database-standards.md). PostgreSQL, per
/// database-design.md: two aggregate tables (<c>product_groups</c>, <c>variants</c>) plus the
/// Transactional Outbox table (<c>product_outbox_events</c>).</summary>
public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

    public DbSet<Variant> Variants => Set<Variant>();

    public DbSet<ProductOutboxEvent> OutboxEvents => Set<ProductOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductDbContext).Assembly);
    }
}
