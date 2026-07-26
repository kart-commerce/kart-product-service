using Kart.Product.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Kart.Product.IntegrationTests.Fixtures;

/// <summary>
/// Real PostgreSQL + MongoDB + RabbitMQ together, for FullPipelineTests - the one test that
/// proves the write (PostgreSQL) -> Outbox -> RabbitMQ -> self-consumption -> read (MongoDB) sync
/// mechanism actually works end to end, not just that each piece works in isolation.
/// </summary>
public sealed class FullPipelineContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("kart_product_test")
        .WithUsername("kart_product_service")
        .WithPassword("changeme")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithImage("mongo:7.0").Build();

    // Testcontainers.RabbitMq generates a random password by default (security-conscious
    // default) - our ConnectionFactory usage only supports HostName/Port (no per-environment
    // credentials, matching every other Kart service's RabbitMqOptions), so the container is
    // pinned to the same guest/guest default the docker-compose.yml rabbitmq service also uses.
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder().WithUsername("guest").WithPassword("guest").Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public IMongoDatabase MongoDatabase => new MongoClient(_mongo.GetConnectionString()).GetDatabase("kart_test");

    public string RabbitMqHostName => _rabbitMq.Hostname;

    public int RabbitMqPort => _rabbitMq.GetMappedPublicPort(5672);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync(), _rabbitMq.StartAsync());

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }

    public ProductDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>().UseNpgsql(PostgresConnectionString).Options;
        return new ProductDbContext(options);
    }
}

[CollectionDefinition("FullPipeline")]
public sealed class FullPipelineCollection : ICollectionFixture<FullPipelineContainerFixture>;
