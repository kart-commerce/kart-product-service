using Kart.Product.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Kart.Product.Api;

/// <summary>
/// Verifies every infra dependency is reachable right after boot, one Connecting/connected log
/// pair per dependency, so a misconfigured or unreachable Postgres/Mongo/RabbitMQ shows up
/// immediately in the startup log instead of surfacing later as the first request's failure
/// (kart-conventions.md, "Database Migrations & Startup Readiness"). Mirrors kart-order-service's/
/// kart-admin-service's own StartupConnectivityChecks.
/// </summary>
public static class StartupConnectivityChecks
{
    public static async Task RunAsync(WebApplication app)
    {
        // WebApplicationFactory-based tests (Contract/Integration) run this same Program.cs but
        // swap out the real client/messaging registrations for in-memory fakes - real
        // connectivity is neither available nor meaningful there, so those factories mark
        // themselves "Testing" and this step is a deliberate no-op for them.
        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        var logger = app.Logger;

        await CheckAsync(logger, "PostgresDB", async () =>
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            await dbContext.Database.CanConnectAsync();
        });

        await CheckAsync(logger, "MongoDB", async () =>
        {
            var database = app.Services.GetRequiredService<IMongoDatabase>();
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        });

        await CheckAsync(logger, "RabbitMQ", () =>
        {
            var connectionFactory = app.Services.GetRequiredService<IConnectionFactory>();
            using var connection = connectionFactory.CreateConnection();
            return Task.CompletedTask;
        });
    }

    private static async Task CheckAsync(ILogger logger, string dependency, Func<Task> connect)
    {
        logger.LogInformation("Connecting Product {Dependency} ...", dependency);
        try
        {
            await connect();
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Failed to connect to Product {Dependency}", dependency);
            throw;
        }

        logger.LogInformation("{Dependency} connected", dependency);
    }
}
