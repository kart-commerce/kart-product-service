using System.Text.Json;

namespace Kart.Product.Infrastructure.Messaging;

/// <summary>
/// Loads and deserializes <c>message-bus-manifest.json</c>. Fails fast (throws) if the file is
/// missing or malformed - there is deliberately no degraded fallback mode, since a missing
/// manifest means the RabbitMQ topology it describes cannot exist (kart-identity-service's
/// proven pattern).
/// </summary>
public static class MessageBusManifestLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static MessageBusManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"message-bus manifest not found at '{path}'. This service cannot start without its RabbitMQ topology definition.",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<MessageBusManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"message-bus manifest at '{path}' deserialized to null.");
    }
}
