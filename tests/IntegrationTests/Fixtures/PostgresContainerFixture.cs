using Kart.Product.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kart.Product.IntegrationTests.Fixtures;

/// <summary>Real PostgreSQL via Testcontainers, migrated once at startup, shared across every
/// test class in the "Postgres" collection.</summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("kart_product_test")
        .WithUsername("kart_product_service")
        .WithPassword("changeme")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ProductDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>().UseNpgsql(ConnectionString).Options;
        return new ProductDbContext(options);
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
