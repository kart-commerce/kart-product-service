using Kart.Product.Application.Common.Models;

namespace Kart.Product.Application.Common.Interfaces;

/// <summary>
/// The MongoDB read side (database-design.md's <c>product_read_model</c>, sharded on
/// <c>category.id</c>). Every write here is a field-scoped partial update - each method updates
/// only the fields its own projector owns, plus <c>lastUpdatedAt</c> - so two concurrent
/// projections of different event types never clobber each other's fields (no version field or
/// optimistic-lock retry loop needed, per database-design.md's concurrency-control decision).
/// </summary>
public interface IProductReadModelRepository
{
    Task<ProductReadModel?> GetBySkuAsync(string sku, CancellationToken cancellationToken);

    /// <summary>Full document upsert - only ever called for <c>ProductCreated</c>, the one event
    /// that materializes a SKU's read-model document for the first time.</summary>
    Task UpsertAsync(ProductReadModel readModel, CancellationToken cancellationToken);

    /// <summary><c>ProductPriceChanged</c> projector: <c>$set: {{ price, lastUpdatedAt }}</c> only.</summary>
    Task UpdatePriceAsync(string sku, decimal amount, string currency, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken);

    /// <summary><c>ProductUpdated</c> projector: <c>$set</c> only the fields named in
    /// <paramref name="fields"/>, plus <c>lastUpdatedAt</c>.</summary>
    Task UpdateFieldsAsync(string sku, IReadOnlyDictionary<string, object?> fields, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken);

    /// <summary><c>ProductDiscontinued</c> projector: <c>$set: {{ status: "Discontinued", lastUpdatedAt }}</c> only.</summary>
    Task MarkDiscontinuedAsync(string sku, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken);

    /// <summary><c>ReviewSubmitted</c>/<c>ReviewUpdated</c> projector: <c>$set: {{ ratingSummary }}</c>
    /// only - never touches <c>price</c>, <c>status</c>, or any catalog field.</summary>
    Task UpdateRatingSummaryAsync(string sku, double avg, int count, CancellationToken cancellationToken);

    /// <summary>
    /// <c>CategoryUpdated</c> projector (Category &amp; Attribute Management (Admin) flow) -
    /// bulk <c>$set: {{ "category.name", lastUpdatedAt }}</c> across every SKU sharing this
    /// categoryId, denormalizing the taxonomy's own name onto every product read-model document
    /// so a client never has to resolve a raw category id itself (previously always null - see
    /// this method's own call site's doc comment). Returns the number of documents matched, for
    /// the caller's own Stage log - zero is a normal, expected outcome (a category with no
    /// products assigned yet), not a failure.
    /// </summary>
    Task<long> UpdateCategoryNameForCategoryAsync(string categoryId, string categoryName, DateTimeOffset lastUpdatedAt, CancellationToken cancellationToken);
}
