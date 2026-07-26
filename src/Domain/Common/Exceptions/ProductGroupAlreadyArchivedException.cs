namespace Kart.Product.Domain.Common.Exceptions;

/// <summary>
/// Archiving is a one-directional, terminal transition (ddd-model.md) - re-archiving an already
/// archived group would re-run the per-sibling discontinue cascade and duplicate-publish events
/// for variants that were already discontinued the first time. Guarded here rather than made a
/// silent no-op so a client retry surfaces as an explicit, debuggable 409 instead of quietly
/// doing nothing.
/// </summary>
public sealed class ProductGroupAlreadyArchivedException(Guid productGroupId)
    : DomainInvariantException($"Product group '{productGroupId}' is already archived.")
{
    public Guid ProductGroupId { get; } = productGroupId;
}
