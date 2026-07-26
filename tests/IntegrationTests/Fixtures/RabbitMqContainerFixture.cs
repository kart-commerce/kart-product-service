using Testcontainers.RabbitMq;
using Xunit;

namespace Kart.Product.IntegrationTests.Fixtures;

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().WithUsername("guest").WithPassword("guest").Build();

    public string HostName => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(5672);

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("RabbitMq")]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqContainerFixture>;
