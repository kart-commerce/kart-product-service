using Kart.Product.Domain.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Product.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md's <c>product_outbox_events</c> table - the Transactional Outbox.</summary>
public sealed class ProductOutboxEventConfiguration : IEntityTypeConfiguration<ProductOutboxEvent>
{
    public void Configure(EntityTypeBuilder<ProductOutboxEvent> builder)
    {
        builder.ToTable("product_outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(e => e.Sku).HasColumnName("sku").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // The Outbox poller's own "find unpublished rows" scan (OutboxRelayHostedService).
        builder.HasIndex(e => e.Id).HasDatabaseName("idx_product_outbox_unpublished").HasFilter("published_at IS NULL");
    }
}
