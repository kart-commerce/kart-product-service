using Serilog.Context;

namespace Kart.Product.Api.Middleware;

/// <summary>Pushes <c>sku</c> - this service's mandatory per-service correlation field
/// (kart-conventions.md's Observability section) - onto every structured log line for a request
/// that carries it in its route (every endpoint here does, except the two product-group
/// collection endpoints). Must run after <c>UseRouting</c> so route values are populated.</summary>
public sealed class ProductContextEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sku = context.GetRouteValue("sku")?.ToString();

        if (sku is not null)
        {
            using (LogContext.PushProperty("sku", sku))
            {
                await next(context);
            }

            return;
        }

        await next(context);
    }
}
