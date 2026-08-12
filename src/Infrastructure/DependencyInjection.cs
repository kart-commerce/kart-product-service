using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Options;
using Kart.Product.Infrastructure.Caching;
using Kart.Product.Infrastructure.Messaging;
using Kart.Product.Infrastructure.Persistence;
using Kart.Product.Infrastructure.ReadModel;
using Kart.Product.Infrastructure.Security;
using Kart.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Kart.Product.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- PostgreSQL write side (database-design.md) ---
        services.AddDbContext<ProductDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ProductDatabase")
                ?? "Host=localhost;Port=5432;Database=kart_product;Username=kart_product_service;Password=changeme"));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IProductGroupRepository, ProductGroupRepository>();
        services.AddScoped<IVariantRepository, VariantRepository>();
        services.AddScoped<IOutboxEventWriter, OutboxEventWriter>();

        // --- MongoDB read side (database-design.md's product_read_model, sharded on category.id) ---
        services.Configure<MongoOptions>(configuration.GetSection("Mongo"));
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoOptions>>().Value;
            var client = new MongoClient(options.ConnectionString);
            return client.GetDatabase(options.Database);
        });
        services.AddScoped<IProductReadModelRepository, MongoProductReadModelRepository>();

        // --- Redis cache-aside + write-through in front of the read model (design-decisions.md,
        // "Caching Strategy for Product Reads") - mirrors kart-category-service's/
        // kart-inventory-service's own ConnectionMultiplexer registration. Connect() only builds
        // the connection (it retries internally), so registering it here is safe even if Redis is
        // unreachable at startup. ---
        services.Configure<ProductCacheOptions>(configuration.GetSection("ProductCache"));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis") ?? "localhost:6379"));
        services.AddScoped<IProductCache, RedisProductCache>();

        // --- Security ---
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPrincipal, HttpCurrentPrincipal>();

        // --- Message-bus-manifest-driven RabbitMQ topology (contracts/message-bus-manifest.json is
        // the single source of truth - nothing here is hardcoded) ---
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddKartMessageBusManifest(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value.ManifestPath);
        services.AddKartRabbitMqConnectionFactory(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqConnectionSettings(options.HostName, options.Port, options.UserName, options.Password);
        });
        services.AddKartRabbitMqTopologyStartup();

        // Registration order = startup order for IHostedService: declare topology once, then the
        // publisher, then the three consumers.
        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<CatalogProjectionConsumerHostedService>();
        services.AddHostedService<ReviewEventsConsumerHostedService>();
        services.AddHostedService<CategoryEventsConsumerHostedService>();

        return services;
    }
}
