using Kart.Product.Domain.Common.Exceptions;
using Kart.Shared.Domain;

namespace Kart.Product.Domain.ProductGroups;

/// <summary>
/// The parent/grouping aggregate (ddd-model.md) - name, description, category, brand. Never
/// itself priced or orderable; the actual sellable, priced unit is <see cref="Variants.Variant"/>,
/// a separate aggregate root. Writes to ProductGroup and Variant are always sequenced saves,
/// never one cross-aggregate transaction (ddd-model.md's transaction-boundary test).
/// </summary>
public sealed class ProductGroup : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string CategoryId { get; private set; } = string.Empty;

    public string? Brand { get; private set; }

    public ProductGroupStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    /// <summary>EF Core materialization constructor.</summary>
    private ProductGroup()
    {
    }

    /// <summary>
    /// ddd-model.md's Cross-Aggregate Interaction flow 1, step 1: saved as <c>Draft</c> - the
    /// caller (CreateProductGroupCommandHandler) flips it to <c>Published</c> via
    /// <see cref="Publish"/> only once the initial Variant has also been saved.
    /// </summary>
    public static ProductGroup Create(
        string name,
        string? description,
        string categoryId,
        string? brand,
        string createdBy,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        return new ProductGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CategoryId = categoryId,
            Brand = brand,
            Status = ProductGroupStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        };
    }

    public void Publish(string updatedBy, DateTimeOffset now)
    {
        Status = ProductGroupStatus.Published;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>
    /// ddd-model.md's Cross-Aggregate Interaction flow 3 (field-edit path). Returns the set of
    /// field names that actually changed, so the caller can fan out one <c>ProductUpdated</c>
    /// per currently-Active sibling Variant naming only those fields (event-contract.md's
    /// <c>changedFields</c> payload member).
    /// </summary>
    public IReadOnlyList<string> UpdateFields(
        string? name,
        string? description,
        string? categoryId,
        string? brand,
        string updatedBy,
        DateTimeOffset now)
    {
        if (Status == ProductGroupStatus.Archived)
        {
            throw new ProductGroupAlreadyArchivedException(Id);
        }

        var changed = new List<string>();

        if (name is not null && name != Name)
        {
            Name = name;
            changed.Add("name");
        }

        if (description is not null && description != Description)
        {
            Description = description;
            changed.Add("description");
        }

        if (categoryId is not null && categoryId != CategoryId)
        {
            CategoryId = categoryId;
            changed.Add("categoryId");
        }

        if (brand is not null && brand != Brand)
        {
            Brand = brand;
            changed.Add("brand");
        }

        if (changed.Count > 0)
        {
            UpdatedBy = updatedBy;
            UpdatedAt = now;
        }

        return changed;
    }

    /// <summary>
    /// One-directional, terminal (ddd-model.md - there is no un-archive path). Guarded against
    /// re-archiving to avoid duplicate-publishing <c>ProductDiscontinued</c> for siblings already
    /// discontinued by a prior archive - see <see cref="ProductGroupAlreadyArchivedException"/>.
    /// </summary>
    public void Archive(string updatedBy, DateTimeOffset now)
    {
        if (Status == ProductGroupStatus.Archived)
        {
            throw new ProductGroupAlreadyArchivedException(Id);
        }

        Status = ProductGroupStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
