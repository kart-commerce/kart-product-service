using System.Text;
using System.Text.Json;
using Kart.Product.Application.Features.ApplyRatingProjection;
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
/// Ticket PRD-6: consumes <c>ReviewSubmitted</c>/<c>ReviewUpdated</c> from the externally-owned
/// <c>review.exchange</c> via this service's own <c>product.review-events.queue</c>, projecting
/// only the <c>ratingSummary</c> field of <c>product_read_model</c> (ADR-0014: a denormalized
/// copy of Review's own canonical rating aggregate, never recomputed/reconciled against Review at
/// read time).
/// </summary>
public sealed class ReviewEventsConsumerHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    MessageBusManifest manifest,
    ILogger<ReviewEventsConsumerHostedService> logger) : BackgroundService
{
    private const string QueueName = "product.review-events.queue";
    private const string ReviewSubmittedRoutingKey = "review.review.submitted";
    private const string ReviewUpdatedRoutingKey = "review.review.updated";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

                logger.LogInformation("Review events consumer listening on {Queue}", QueueName);

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
                logger.LogWarning(exception, "Review events consumer lost its RabbitMQ connection - reconnecting in {Delay}", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageAsync(IModel channel, QueueDefinition queue, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(delivery.Body.ToArray());
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // Not delivery.RoutingKey directly - a retry-ladder bounce overwrites it with the
            // retry-tier queue name by the time RabbitMQ redelivers to this queue.
            var routingKey = RetryLadderDispatcher.GetEffectiveRoutingKey(delivery);

            ApplyRatingProjectionCommand command = routingKey switch
            {
                ReviewSubmittedRoutingKey => FromSubmitted(json),
                ReviewUpdatedRoutingKey => FromUpdated(json),
                _ => throw new InvalidOperationException($"Unrecognized routing key '{routingKey}' on {QueueName}."),
            };

            await sender.Send(command, cancellationToken);

            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            RetryLadderDispatcher.HandleFailure(channel, delivery, queue, logger, exception);
        }
    }

    private static ApplyRatingProjectionCommand FromSubmitted(string json)
    {
        var payload = JsonSerializer.Deserialize<ReviewSubmittedPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("ReviewSubmitted payload deserialized to null.");
        return new ApplyRatingProjectionCommand(payload.Sku, payload.Rating, null, null);
    }

    private static ApplyRatingProjectionCommand FromUpdated(string json)
    {
        var payload = JsonSerializer.Deserialize<ReviewUpdatedPayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("ReviewUpdated payload deserialized to null.");
        return new ApplyRatingProjectionCommand(payload.Sku, null, payload.OldRating, payload.NewRating);
    }
}
