using System.Text;
using Kart.Product.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Kart.Shared.Messaging;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>
/// The Outbox relay: polls <c>product_outbox_events</c> for unpublished rows and publishes each
/// to the exchange/routing key the manifest resolves for its event type - never hardcoded. Owns
/// its own reconnect loop; a RabbitMQ outage never crashes the process, it just retries.
/// </summary>
public sealed class OutboxRelayHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
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
                var factory = new ConnectionFactory { HostName = options.Value.HostName, Port = options.Value.Port, DispatchConsumersAsync = true };
                using var connection = factory.CreateConnection();
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
                logger.LogWarning(exception, "Outbox relay lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
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
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.Id.ToString();
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: manifest.ExchangeFor(outboxEvent.EventType),
                routingKey: manifest.RoutingKeyFor(outboxEvent.EventType),
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(publishedAt, "system:product-outbox-poller");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Relayed {Count} outbox event(s) to {Exchange}", batch.Count, manifest.Exchanges.FirstOrDefault()?.Name);
    }
}
