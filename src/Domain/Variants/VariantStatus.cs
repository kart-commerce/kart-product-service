namespace Kart.Product.Domain.Variants;

/// <summary>ddd-model.md: Discontinued is one-directional and terminal - there is no un-discontinue path.</summary>
public enum VariantStatus
{
    Active,
    Discontinued,
}
