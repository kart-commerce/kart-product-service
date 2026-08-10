using FluentValidation.AspNetCore;
using Kart.Product.Api;
using Kart.Product.Api.Middleware;
using Kart.Product.Api.Security;
using Kart.Product.Application;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Domain.Common.Exceptions;
using Kart.Product.Infrastructure;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddKartGlobalConfig();

builder.AddKartObservability("kart-product-service");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProductAuthentication();

builder.Services.AddKartErrorHandling(options => options
    .Map<SkuAlreadyExistsException>(StatusCodes.Status409Conflict, "SKU_ALREADY_EXISTS")
    .Map<ProductGroupNotFoundException>(StatusCodes.Status404NotFound, "PRODUCT_GROUP_NOT_FOUND")
    .Map<VariantNotFoundException>(StatusCodes.Status404NotFound, "VARIANT_NOT_FOUND")
    .Map<MixedUpdateRequestException>(StatusCodes.Status409Conflict, "MIXED_UPDATE_REQUEST")
    .Map<ProductGroupAlreadyArchivedException>(StatusCodes.Status409Conflict, "PRODUCT_GROUP_ALREADY_ARCHIVED")
    .Map<VariantAlreadyDiscontinuedException>(StatusCodes.Status409Conflict, "VARIANT_ALREADY_DISCONTINUED")
    .Map<VariantDiscontinuedException>(StatusCodes.Status409Conflict, "VARIANT_DISCONTINUED"));

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await StartupConnectivityChecks.RunAsync(app);

// The global exception handler is the only place any exception reaching the HTTP boundary is
// caught and translated - registered first so it wraps everything downstream.
app.UseKartErrorHandling();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseMiddleware<ProductContextEnrichmentMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.Run();

/// <summary>Exposes the implicitly-generated Program class to WebApplicationFactory&lt;Program&gt; in ContractTests/IntegrationTests.</summary>
public partial class Program;
