using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>Declares this service's entire RabbitMQ topology once at boot, from the manifest.
/// A RabbitMQ outage at boot never crashes the process - only logs a warning, since
/// <see cref="OutboxRelayHostedService"/> and the consumers each own their own independent
/// reconnect loop and will (re)declare the topology themselves once the broker is reachable.</summary>
public sealed class RabbitMqTopologyStartupHostedService(
    IOptions<RabbitMqOptions> options,
    MessageBusManifest manifest,
    ILogger<RabbitMqTopologyStartupHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var factory = new ConnectionFactory { HostName = options.Value.HostName, Port = options.Value.Port, DispatchConsumersAsync = true };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            RabbitMqTopologyProvisioner.Declare(channel, manifest);

            logger.LogInformation("Declared RabbitMQ topology for {Service} at startup", manifest.Service);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not declare RabbitMQ topology at startup for {Service} - will be retried by the relay/consumer hosted services", manifest.Service);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
