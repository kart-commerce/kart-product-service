using FluentValidation;
using MediatR;

namespace Kart.Product.Application.Common.Behaviours;

/// <summary>Runs every registered <see cref="IValidator{T}"/> for this request, aggregates
/// failures, and throws a single <see cref="ValidationException"/> if any fail - deliberately
/// uncaught here; translated to a 400 response exactly once, by the global exception handler.</summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
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
            throw new ValidationException(failures);
        }

        return await next();
    }
}
