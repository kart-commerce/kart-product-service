using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace Kart.Product.IntegrationTests.Fixtures;

/// <summary>
/// Real MongoDB via Testcontainers - a single node, not the sharded cluster docker-compose.yml
/// stands up for local dev. Sharding is a deployment-topology concern (verified via
/// scripts/init-mongo-cluster.sh, not re-provable in a unit-speed test); what these tests need to
/// prove is that MongoProductReadModelRepository's field-scoped partial updates behave correctly
/// against a real Mongo server, which a single node demonstrates identically to a sharded one.
/// </summary>
public sealed class MongoContainerFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().WithImage("mongo:7.0").Build();

    public IMongoDatabase CreateDatabase() => new MongoClient(_container.GetConnectionString()).GetDatabase("kart_test");

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("Mongo")]
public sealed class MongoCollection : ICollectionFixture<MongoContainerFixture>;
