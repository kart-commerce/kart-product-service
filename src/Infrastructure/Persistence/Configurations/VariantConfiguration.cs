using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Product.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md's <c>variants</c> table - PK is <c>sku</c> itself, no surrogate
/// key (see the Domain type's own remarks on why).</summary>
public sealed class VariantConfiguration : IEntityTypeConfiguration<Variant>
{
    public void Configure(EntityTypeBuilder<Variant> builder)
    {
        builder.ToTable("variants", t => t.HasCheckConstraint("CK_variants_status", "status IN ('Active', 'Discontinued')"));

        builder.HasKey(v => v.Sku);
        builder.Property(v => v.Sku)
            .HasColumnName("sku")
            .HasConversion(sku => sku.Value, value => Sku.From(value));

        builder.Property(v => v.ProductGroupId).HasColumnName("product_group_id").IsRequired();

        // Restrict, not Cascade: Domain Invariants (ddd-model.md) - ProductGroup is only ever
        // soft-archived, never hard-deleted, so a cascading FK delete should never be reachable
        // in practice. Restrict makes that invariant a DB-level guarantee too, not just an
        // application-level convention.
        builder.HasOne<ProductGroup>().WithMany().HasForeignKey(v => v.ProductGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(v => v.Price, price =>
        {
            price.Property(m => m.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)").IsRequired();
            price.Property(m => m.Currency).HasColumnName("price_currency").IsRequired();
        });
        builder.Navigation(v => v.Price).IsRequired();

        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.Size).HasColumnName("size");
        builder.Property(v => v.Color).HasColumnName("color");

        builder.Property(v => v.ExtendedAttributes)
            .HasColumnName("extended_attributes")
            .HasColumnType("jsonb")
            .HasConversion(JsonDictionaryValueConverter.Converter, JsonDictionaryValueConverter.Comparer)
            .IsRequired();

        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(v => v.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // Archive cascade (ddd-model.md flow 3) and parent-edit fan-out - "every currently-Active sibling Variant".
        builder.HasIndex(v => v.ProductGroupId).HasDatabaseName("idx_variants_product_group_id");

        // Browse/facet queries at 100M-SKU scale (requirement-spec.md §2) - partial, since a
        // category without a size/color concept leaves them null.
        builder.HasIndex(v => v.Size).HasDatabaseName("idx_variants_size").HasFilter("size IS NOT NULL");
        builder.HasIndex(v => v.Color).HasDatabaseName("idx_variants_color").HasFilter("color IS NOT NULL");
    }
}
