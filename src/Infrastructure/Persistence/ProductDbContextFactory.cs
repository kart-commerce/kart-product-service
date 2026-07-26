using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kart.Product.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> tooling create a <see cref="ProductDbContext"/> without the
/// full Api host/JWT config, reading the connection string from an env var with a local dev
/// default (mirrors kart-inventory-service/kart-category-service's own design-time factory).</summary>
public sealed class ProductDbContextFactory : IDesignTimeDbContextFactory<ProductDbContext>
{
    public ProductDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODUCT_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_product;Username=kart_product_service;Password=changeme";

        var optionsBuilder = new DbContextOptionsBuilder<ProductDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ProductDbContext(optionsBuilder.Options);
    }
}
