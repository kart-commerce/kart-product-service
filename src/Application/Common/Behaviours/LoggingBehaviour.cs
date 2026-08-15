using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Common.Behaviours;

/// <summary>
/// Logs every request's completion and elapsed time. Deliberately never logs request/response
/// payload values, and deliberately never catches exceptions - that's the global exception
/// handler's job (Kart.Shared.ErrorHandling), so an exception is logged exactly once, not twice.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();
        logger.LogInformation("{RequestName} completed in {ElapsedMilliseconds}ms", requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }
}
