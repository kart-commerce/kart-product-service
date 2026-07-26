using Kart.Product.Application.Common.Interfaces;
using Kart.Product.ContractTests.Fakes;
using Kart.Product.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Kart.Product.ContractTests;

/// <summary>
/// Boots the real Api + Application pipeline (Program.cs unchanged), swapping PostgreSQL/MongoDB/
/// RabbitMQ for in-memory fakes and replacing JWT bearer auth with TestAuthenticationHandler -
/// asserts HTTP wire-shape only (status codes, JSON field names, AdminOrPartner gating), never
/// touching a real database/broker. Mirrors kart-inventory-service/kart-category-service's
/// ContractTests factory.
/// </summary>
public sealed class ProductApiFactory : WebApplicationFactory<Program>
{
    public InMemoryProductGroupRepository ProductGroupRepository { get; } = new();

    public InMemoryVariantRepository VariantRepository { get; } = new();

    public InMemoryProductReadModelRepository ReadModelRepository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // None of the hosted services Infrastructure registers (RabbitMQ topology/outbox
            // relay/self-consumption/review-events consumers) can reach a real Postgres/Mongo/
            // RabbitMQ here.
            services.RemoveAll(typeof(IHostedService));

            services.RemoveAll(typeof(DbContextOptions<ProductDbContext>));
            services.RemoveAll(typeof(ProductDbContext));
            services.RemoveAll(typeof(IMongoDatabase));

            services.RemoveAll(typeof(IProductGroupRepository));
            services.AddSingleton<IProductGroupRepository>(ProductGroupRepository);

            services.RemoveAll(typeof(IVariantRepository));
            services.AddSingleton<IVariantRepository>(VariantRepository);

            services.RemoveAll(typeof(IProductReadModelRepository));
            services.AddSingleton<IProductReadModelRepository>(ReadModelRepository);

            services.RemoveAll(typeof(IUnitOfWork));
            services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();

            services.RemoveAll(typeof(IOutboxEventWriter));
            services.AddSingleton<IOutboxEventWriter, NullOutboxEventWriter>();

            // Replaces the JWT bearer scheme registered by Program.cs's AddProductAuthentication -
            // this AddAuthentication(defaultScheme:) call runs after Program.cs's own, so it wins
            // for AuthenticationOptions.DefaultScheme/DefaultAuthenticateScheme. The AdminOrPartner
            // policy is untouched - it checks the `scope` claim regardless of which handler
            // produced it.
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
        });
    }
}
