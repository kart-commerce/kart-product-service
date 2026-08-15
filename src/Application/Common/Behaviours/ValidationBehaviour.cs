using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Common.Behaviours;

/// <summary>Runs every registered <see cref="IValidator{T}"/> for this request, aggregates
/// failures, and throws a single <see cref="ValidationException"/> if any fail - deliberately
/// uncaught here; translated to a 400 response exactly once, by the global exception handler.</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            var requestName = typeof(TRequest).Name;

            // Checkpoint-logging taxonomy stage 4 ("<Rule>ValidationFailed", logged at Warning
            // with the reason before throwing) generalized here for every FluentValidation
            // validator on the platform, rather than duplicated per handler - the
            // ValidationException itself is still logged once more, generically, at the API
            // boundary by the global exception handler; this line is the one that's greppable by
            // Stage and carries the actual field-level reasons (checkpoint-logging-standard.md).
            logger.LogWarning(
                "Stage {Stage}: {RequestName} rejected - {Errors}",
                $"{requestName}ValidationFailed",
                requestName,
                string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

            throw new ValidationException(failures);
        }

        return await next();
    }
}
