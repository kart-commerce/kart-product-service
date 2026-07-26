using Kart.Product.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Features.ApplyRatingProjection;

/// <summary>
/// Maintains <c>product_read_model.ratingSummary</c> (avg/count) incrementally from Review's
/// per-review delta events - ADR-0014: a denormalized projection, never canonical, never
/// reconciled synchronously against Review's own aggregate at read time. Field-scoped: never
/// touches <c>price</c>, <c>status</c>, or any catalog field (database-design.md).
/// </summary>
public sealed class ApplyRatingProjectionCommandHandler(
    IProductReadModelRepository readModelRepository,
    ILogger<ApplyRatingProjectionCommandHandler> logger) : IRequestHandler<ApplyRatingProjectionCommand>
{
    public async Task Handle(ApplyRatingProjectionCommand request, CancellationToken cancellationToken)
    {
        var existing = await readModelRepository.GetBySkuAsync(request.Sku, cancellationToken);
        if (existing is null)
        {
            // tickets.md: "a product_read_model document must exist for the projector to
            // update" - PRD-1 (ProductCreated) always lands first in practice, but at-least-once
            // delivery means this can still race a not-yet-materialized document. A no-op here is
            // safe: the message is acked either way, and Review's own canonical rating is
            // unaffected - only this denormalized copy would be briefly behind, which the
            // platform already accepts for this projection (ADR-0014).
            logger.LogWarning("No product_read_model document for SKU {Sku} yet - skipping rating projection", request.Sku);
            return;
        }

        var (avg, count) = existing.RatingSummary switch
        {
            var summary when request.SubmittedRating is { } submitted => Submit(summary.Avg, summary.Count, submitted),
            var summary when request.OldRating is { } oldRating && request.NewRating is { } newRating => Update(summary.Avg, summary.Count, oldRating, newRating),
            _ => (existing.RatingSummary.Avg, existing.RatingSummary.Count),
        };

        await readModelRepository.UpdateRatingSummaryAsync(request.Sku, avg, count, cancellationToken);
    }

    private static (double Avg, int Count) Submit(double avg, int count, double submittedRating)
    {
        var newCount = count + 1;
        var newAvg = ((avg * count) + submittedRating) / newCount;
        return (newAvg, newCount);
    }

    private static (double Avg, int Count) Update(double avg, int count, double oldRating, double newRating)
    {
        if (count == 0)
        {
            return (newRating, count);
        }

        var newAvg = ((avg * count) - oldRating + newRating) / count;
        return (newAvg, count);
    }
}
