using Kart.Product.Domain.ProductGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Product.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md's <c>product_groups</c> table.</summary>
public sealed class ProductGroupConfiguration : IEntityTypeConfiguration<ProductGroup>
{
    public void Configure(EntityTypeBuilder<ProductGroup> builder)
    {
        builder.ToTable("product_groups", t => t.HasCheckConstraint("CK_product_groups_status", "status IN ('Draft', 'Published', 'Archived')"));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Name).HasColumnName("name").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(p => p.Brand).HasColumnName("brand");

        builder.Property(p => p.ImageUrl)
            .HasColumnName("image_url")
            .HasConversion(imageUrl => imageUrl.Value, value => ImageUrl.Create(value))
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").IsRequired();

        // Any future "all product groups in category X" admin/reporting query (database-design.md's indexing rationale).
        builder.HasIndex(p => p.CategoryId).HasDatabaseName("idx_product_groups_category_id");
    }
}
