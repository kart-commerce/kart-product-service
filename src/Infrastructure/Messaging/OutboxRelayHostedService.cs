using System.Text;
using Kart.Product.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>
/// The Outbox relay: polls <c>product_outbox_events</c> for unpublished rows and publishes each
/// to the exchange/routing key the manifest resolves for its event type - never hardcoded. Owns
/// its own reconnect loop; a RabbitMQ outage never crashes the process, it just retries.
/// </summary>
public sealed class OutboxRelayHostedService(
    IServiceScopeFactory scopeFactory,
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    ILogger<OutboxRelayHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();

                RabbitMqTopologyProvisioner.Declare(channel, manifest);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await RelayBatchAsync(channel, stoppingToken);
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Stage {Stage}: outbox relay lost its RabbitMQ connection - reconnecting in {Delay}", "RabbitMqPublishRetryScheduled", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RelayBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        var batch = await dbContext.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return;
        }

        var publishedAt = DateTimeOffset.UtcNow;

        foreach (var outboxEvent in batch)
        {
            // Every event type this outbox ever carries (ProductCreated/PriceChanged/Updated/
            // Discontinued) belongs to this flow - no per-row category filter needed the way
            // admin-service's mixed-category outbox needs one.
            using var flowScope = KartFlowContext.Push("ProductCatalogManagementAdmin");

            var exchange = manifest.ExchangeFor(outboxEvent.EventType);
            var routingKey = manifest.RoutingKeyFor(outboxEvent.EventType);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.Id.ToString();
            properties.ContentType = "application/json";

            using var activity = RabbitMqTraceContext.StartPublishActivityFromStoredTraceParent(exchange, routingKey, outboxEvent.TraceParent, properties);

            channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(publishedAt, "system:product-outbox-poller");

            logger.LogInformation(
                "Stage {Stage}: outbox event {OutboxEventId} ({EventType}) for sku {Sku} published to {Exchange}/{RoutingKey}",
                "OutboxEventPublished",
                outboxEvent.Id,
                outboxEvent.EventType,
                outboxEvent.Sku,
                exchange,
                routingKey);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Relayed {Count} outbox event(s) to {Exchange}", batch.Count, manifest.Exchanges.FirstOrDefault()?.Name);
    }
}
