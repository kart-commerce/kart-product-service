using Kart.Product.Domain.Common.Exceptions;

namespace Kart.Product.Domain.Variants;

/// <summary>
/// The actual sellable, priced, SKU-identified unit (ddd-model.md) - a separate aggregate root
/// from <see cref="ProductGroups.ProductGroup"/>, never sharing a transaction with it. Identity
/// is the SKU itself, not a surrogate Guid (database-design.md explains why: every access path -
/// the uniqueness invariant, the PATCH write, the read-model projector's upsert key - is a point
/// lookup by <c>sku</c> and nothing else) - so this does not extend
/// <see cref="Kart.Shared.Domain.AggregateRoot"/>, which is Guid-keyed.
/// </summary>
public sealed class Variant
{
    public Sku Sku { get; private set; }

    /// <summary>Guid, not a strongly typed id: it must stay comparable, at the EF Core
    /// relationship level, to <see cref="ProductGroups.ProductGroup"/>.Id, which is itself
    /// Guid - inherited from the shared, cross-service <c>AggregateRoot</c> base, which is out
    /// of this service's scope alone to retype (see the accompanying primitive-obsession
    /// review). A wrapped id here without one there is a real EF Core limitation, not a style
    /// choice: EF Core requires a foreign key's CLR type to match its principal key's CLR type,
    /// value converters notwithstanding.</summary>
    public Guid ProductGroupId { get; private set; }

    public Money Price { get; private set; } = null!;

    public VariantStatus Status { get; private set; }

    public string? Size { get; private set; }

    public string? Color { get; private set; }

    public IReadOnlyDictionary<string, object?> ExtendedAttributes { get; private set; } = new Dictionary<string, object?>();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    public ProductAttributes Attributes => new(Size, Color, ExtendedAttributes);

    /// <summary>EF Core materialization constructor.</summary>
    private Variant()
    {
    }

    public static Variant Create(
        Sku sku,
        Guid productGroupId,
        Money price,
        ProductAttributes attributes,
        string createdBy,
        DateTimeOffset now)
    {
        return new Variant
        {
            Sku = sku,
            ProductGroupId = productGroupId,
            Price = price,
            Status = VariantStatus.Active,
            Size = attributes.Size,
            Color = attributes.Color,
            ExtendedAttributes = attributes.ExtendedAttributes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        };
    }

    /// <summary>Returns the price in effect before the change, for the caller to build the
    /// <c>ProductPriceChanged</c> event payload's <c>oldPrice</c>/<c>newPrice</c> pair.</summary>
    public Money ChangePrice(Money newPrice, string updatedBy, DateTimeOffset now)
    {
        EnsureMutable();

        var oldPrice = Price;
        Price = newPrice;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
        return oldPrice;
    }

    public void UpdateAttributes(ProductAttributes attributes, string updatedBy, DateTimeOffset now)
    {
        EnsureMutable();

        Size = attributes.Size;
        Color = attributes.Color;
        ExtendedAttributes = attributes.ExtendedAttributes;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>
    /// One-directional, terminal (ddd-model.md). Guarded against re-discontinuing to avoid
    /// duplicate-publishing <c>ProductDiscontinued</c> - see
    /// <see cref="VariantAlreadyDiscontinuedException"/>.
    /// </summary>
    public void Discontinue(string updatedBy, DateTimeOffset now)
    {
        if (Status == VariantStatus.Discontinued)
        {
            throw new VariantAlreadyDiscontinuedException(Sku);
        }

        Status = VariantStatus.Discontinued;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    private void EnsureMutable()
    {
        if (Status == VariantStatus.Discontinued)
        {
            throw new VariantDiscontinuedException(Sku);
        }
    }
}
