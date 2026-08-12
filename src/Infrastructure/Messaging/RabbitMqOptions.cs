namespace Kart.Product.Infrastructure.Messaging;

/// <summary>Binds the <c>"RabbitMq"</c> config section - the only two messaging settings not
/// described by the manifest itself.</summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    /// <summary>Defaults to RabbitMQ's standard AMQP port - overridden in tests, where
    /// Testcontainers maps the broker to a randomized host port.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Left unset to fall back to RabbitMQ.Client's own guest/guest default
    /// (loopback-only brokers) - mirrors kart-admin-service's RabbitMqOptions.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
