using System.Text;
using Kart.Product.Application.Features.ProjectCatalogEvent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Kart.Shared.Messaging;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>
/// This service's own self-consumption of the four events it publishes onto
/// <c>product.exchange</c> (<c>ProductCreated</c>/<c>ProductPriceChanged</c>/<c>ProductUpdated</c>/
/// <c>ProductDiscontinued</c>), via its own <c>product.catalog-projection.queue</c>. This is the
/// write-side (PostgreSQL) to read-side (MongoDB <c>product_read_model</c>) sync mechanism: write
/// -&gt; outbox -&gt; publish -&gt; this queue -&gt; field-scoped Mongo upsert - reusing the exact
/// same manifest-driven topology/retry-ladder/DLQ machinery as any other consumer, rather than a
/// bespoke in-process sync path.
/// </summary>
public sealed class CatalogProjectionConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    MessageBusManifest manifest,
    ILogger<CatalogProjectionConsumerHostedService> logger) : BackgroundService
{
    private const string QueueName = "product.catalog-projection.queue";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = options.Value.HostName, Port = options.Value.Port, DispatchConsumersAsync = true };
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                RabbitMqTopologyProvisioner.Declare(channel, manifest);
                channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                var queue = manifest.GetQueue(QueueName);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, delivery) => await OnMessageAsync(channel, queue, delivery, stoppingToken);

                channel.BasicConsume(QueueName, autoAck: false, consumer);

                logger.LogInformation("Catalog projection consumer listening on {Queue}", QueueName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Catalog projection consumer lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageAsync(IModel channel, QueueDefinition queue, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var eventType = ResolveEventType(delivery, manifest);
            var payloadJson = Encoding.UTF8.GetString(delivery.Body.ToArray());

            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ProjectCatalogEventCommand(eventType, payloadJson), cancellationToken);

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            RetryLadderDispatcher.HandleFailure(channel, delivery, queue, logger, exception);
        }
    }

    /// <summary>The routing key this message originally arrived with (<c>product.product.created</c>,
    /// etc. - NOT necessarily <see cref="BasicDeliverEventArgs.RoutingKey"/>, which a retry-ladder
    /// bounce overwrites with the retry-tier queue name; see
    /// <see cref="RetryLadderDispatcher.GetEffectiveRoutingKey"/>) maps 1:1 back to the manifest's
    /// <c>publishedEvents</c> event type - resolved here instead of hardcoding a routing-key-to-
    /// event-type table a second time.</summary>
    private static string ResolveEventType(BasicDeliverEventArgs delivery, MessageBusManifest manifest)
    {
        var routingKey = RetryLadderDispatcher.GetEffectiveRoutingKey(delivery);
        var match = manifest.PublishedEvents.FirstOrDefault(e => e.RoutingKey == routingKey);
        return match?.EventType ?? throw new InvalidOperationException($"No publishedEvents entry for routing key '{routingKey}'.");
    }
}
