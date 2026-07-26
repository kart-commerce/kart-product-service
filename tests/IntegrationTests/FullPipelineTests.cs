using FluentAssertions;
using Kart.Product.Application;
using Kart.Product.Application.Features.CreateProductGroup;
using Kart.Product.Domain.Variants;
using Kart.Product.Infrastructure;
using Kart.Product.IntegrationTests.Fakes;
using Kart.Product.IntegrationTests.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Kart.Product.IntegrationTests;

/// <summary>
/// Proves the actual sync mechanism this service is built around: a write to PostgreSQL becomes
/// visible in the MongoDB read model purely by going through the real Outbox -> RabbitMQ ->
/// self-consumption pipeline (no direct call from the write handler into Mongo) - using the exact
/// same DI wiring (<c>AddInfrastructure</c>) and hosted services Program.cs registers, against
/// real PostgreSQL/MongoDB/RabbitMQ containers.
/// </summary>
[Collection("FullPipeline")]
public sealed class FullPipelineTests(FullPipelineContainerFixture fixture)
{
    private async Task<(ServiceProvider Provider, List<IHostedService> HostedServices)> StartPipelineAsync()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProductDatabase"] = fixture.PostgresConnectionString,
            ["RabbitMq:HostName"] = fixture.RabbitMqHostName,
            ["RabbitMq:Port"] = fixture.RabbitMqPort.ToString(),
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // The container fixture already owns the Mongo connection (randomized Testcontainers
        // port) - swap in that exact IMongoDatabase instead of re-deriving one from config.
        services.RemoveAll<MongoDB.Driver.IMongoDatabase>();
        services.AddSingleton(fixture.MongoDatabase);

        services.RemoveAll<Kart.Product.Application.Common.Interfaces.ICurrentPrincipal>();
        services.AddSingleton<Kart.Product.Application.Common.Interfaces.ICurrentPrincipal>(new FixedCurrentPrincipal("admin-1"));

        var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        // RabbitMqTopologyStartupHostedService declares the topology synchronously in StartAsync;
        // give the relay/consumer BackgroundServices a moment to open their own connections and
        // start consuming before the test publishes anything.
        await Task.Delay(TimeSpan.FromSeconds(2));

        return (provider, hostedServices);
    }

    [Fact]
    public async Task CreateProductGroup_EventuallyProjectsIntoMongo_ThroughTheRealBus()
    {
        var (provider, hostedServices) = await StartPipelineAsync();
        try
        {
            const string sku = "sku-full-pipeline";
            var sender = provider.GetRequiredService<ISender>();

            await sender.Send(new CreateProductGroupCommand("Wireless Mouse", "desc", "cat-1", "Acme", sku, new Money(24.99m, "USD"), ProductAttributes.Empty));

            var readModelRepository = provider.GetRequiredService<Kart.Product.Application.Common.Interfaces.IProductReadModelRepository>();

            Kart.Product.Application.Common.Models.ProductReadModel? projected = null;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                projected = await readModelRepository.GetBySkuAsync(sku, CancellationToken.None);
                if (projected is not null)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }

            if (projected is null)
            {
                // Pinpoints which stage broke, if this ever regresses: was the outbox row even
                // written, and had the relay published it yet?
                await using var dbContext = fixture.CreateDbContext();
                var outboxRow = dbContext.OutboxEvents.AsNoTracking().FirstOrDefault(e => e.Sku == sku);
                Console.WriteLine($"Outbox row present: {outboxRow is not null}, publishedAt: {outboxRow?.PublishedAt}");
            }

            projected.Should().NotBeNull("the write should have flowed write -> outbox -> RabbitMQ -> self-consumption -> Mongo within the poll window");
            projected!.Name.Should().Be("Wireless Mouse");
            projected.Category.Id.Should().Be("cat-1");
            projected.PriceAmount.Should().Be(24.99m);
        }
        finally
        {
            foreach (var hostedService in hostedServices)
            {
                await hostedService.StopAsync(CancellationToken.None);
            }

            await provider.DisposeAsync();
        }
    }
}
