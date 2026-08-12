namespace Kart.Product.Domain.Events;

/// <summary>
/// event-contract.md (kart-category-service) CategoryUpdated payload - consumed here (Category
/// &amp; Attribute Management (Admin) flow) purely to denormalize <c>category.name</c> onto every
/// product sharing that categoryId (database-design.md's <c>product_read_model</c>, previously
/// always null - see ProjectCatalogEventCommandHandler.ProjectCreatedAsync's own historical
/// comment). This service never derives behavior from parentId/path/displayOrder/operation -
/// they're deserialized only because System.Text.Json's `JsonSerializerDefaults.Web` requires the
/// shape to round-trip cleanly, not because this consumer uses them.
/// </summary>
public sealed record CategoryUpdatedDomainEvent(
    string CategoryId,
    string Name,
    string? ParentId,
    IReadOnlyList<string> Path,
    int DisplayOrder,
    string Operation,
    DateTimeOffset OccurredAt);
