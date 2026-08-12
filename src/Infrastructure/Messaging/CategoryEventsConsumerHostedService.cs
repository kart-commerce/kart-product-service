using System.Text;
using Kart.Product.Application.Features.ProjectCategoryEvent;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>
/// Category &amp; Attribute Management (Admin) flow: consumes kart-category-service's own
/// CategoryUpdated event (published on the external <c>category.exchange</c>, not this service's
/// own <c>product.exchange</c>) via <c>product.category-projection.queue</c>, denormalizing the
/// taxonomy's own name onto every product sharing that categoryId (database-design.md's
/// <c>product_read_model.category.name</c>, previously always null). Mirrors
/// <see cref="CatalogProjectionConsumerHostedService"/>'s own shape exactly - same
/// manifest-driven topology/retry-ladder/DLQ machinery, a separate queue/hosted service because
/// this is a genuinely different upstream exchange/event, not this service's own self-consumption.
/// </summary>
public sealed class CategoryEventsConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    ILogger<CategoryEventsConsumerHostedService> logger) : BackgroundService
{
    private const string QueueName = "product.category-projection.queue";
    private const string FlowName = "CategoryAttributeManagementAdmin";
    private const string CategoryUpdatedRoutingKey = "category.category.updated";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();

                RabbitMqTopologyProvisioner.Declare(channel, manifest);
                channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                var queue = manifest.GetQueue(QueueName);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, delivery) => await OnMessageAsync(channel, queue, delivery, stoppingToken);

                channel.BasicConsume(QueueName, autoAck: false, consumer);

                logger.LogInformation("Category events consumer listening on {Queue}", QueueName);

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
                logger.LogWarning(exception, "Category events consumer lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageAsync(IModel channel, QueueDefinition queue, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        using var flowScope = KartFlowContext.Push(FlowName);
        using var activity = RabbitMqTraceContext.StartConsumeActivity(QueueName, delivery.BasicProperties);

        try
        {
            // Not delivery.RoutingKey directly - a retry-ladder bounce overwrites it with the
            // retry-tier queue name by the time RabbitMQ redelivers to this queue. Switched
            // directly on the routing key (not manifest.PublishedEvents, which only ever lists
            // events *this* service publishes) - same pattern as ReviewEventsConsumerHostedService
            // for the same reason: category.exchange is an externally-owned exchange.
            var routingKey = RetryLadderDispatcher.GetEffectiveRoutingKey(delivery);
            var eventType = routingKey switch
            {
                CategoryUpdatedRoutingKey => "CategoryUpdated",
                _ => throw new InvalidOperationException($"Unrecognized routing key '{routingKey}' on {QueueName}."),
            };
            var payloadJson = Encoding.UTF8.GetString(delivery.Body.ToArray());

            logger.LogInformation(
                "Stage {Stage}: {EventType} consumed from {Queue}",
                "CategoryUpdatedConsumed",
                eventType,
                QueueName);

            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ProjectCategoryEventCommand(eventType, payloadJson), cancellationToken);

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            RetryLadderDispatcher.HandleFailure(channel, delivery, queue, logger, exception);
        }
    }

}
