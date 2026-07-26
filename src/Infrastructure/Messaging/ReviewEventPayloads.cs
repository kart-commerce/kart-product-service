namespace Kart.Product.Infrastructure.Messaging;

/// <summary>kart-review-service's event-contract.md payload shapes for the two events this
/// service consumes - owned by Review, not us; kept local to this consumer rather than in the
/// manifest, since a manifest only ever describes topology, never payload schemas.</summary>
public sealed record ReviewSubmittedPayload(string OrderId, string Sku, double Rating, string ReviewId, string UserId);

public sealed record ReviewUpdatedPayload(string OrderId, string Sku, double OldRating, double NewRating);
