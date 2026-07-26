namespace Kart.Product.Domain.Common.Exceptions;

/// <summary>
/// Discontinuation is one-directional and terminal (ddd-model.md - "no un-discontinue path").
/// Guarded the same way as <see cref="ProductGroupAlreadyArchivedException"/>, and for the same
/// reason: re-discontinuing would duplicate-publish <c>ProductDiscontinued</c>.
/// </summary>
public sealed class VariantAlreadyDiscontinuedException(string sku)
    : DomainInvariantException($"Variant '{sku}' is already discontinued.")
{
    public string Sku { get; } = sku;
}
