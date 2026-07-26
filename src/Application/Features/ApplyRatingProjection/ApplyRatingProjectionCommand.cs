using MediatR;

namespace Kart.Product.Application.Features.ApplyRatingProjection;

/// <summary>PRD-6: driven by <c>ReviewSubmitted</c> (<see cref="SubmittedRating"/> set) or
/// <c>ReviewUpdated</c> (<see cref="OldRating"/>/<see cref="NewRating"/> set) - exactly one of the
/// two shapes is populated per call, enforced by the two call sites in
/// ReviewEventsConsumerHostedService rather than re-validated here (this is an internal,
/// bus-driven command, never reachable from an HTTP request).</summary>
public sealed record ApplyRatingProjectionCommand(string Sku, double? SubmittedRating, double? OldRating, double? NewRating) : IRequest;
