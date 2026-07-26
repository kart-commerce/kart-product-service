namespace Kart.Product.Domain.ProductGroups;

/// <summary>ddd-model.md: Draft -> Published on initial creation; Archived is a one-directional, forward-only transition.</summary>
public enum ProductGroupStatus
{
    Draft,
    Published,
    Archived,
}
