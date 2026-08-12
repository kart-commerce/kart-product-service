namespace Kart.Product.Domain.Outbox;

/// <summary>
/// Transactional Outbox row (database-design.md's <c>product_outbox_events</c>) - written in the
/// same PostgreSQL transaction as the Variant/ProductGroup write that caused it, guaranteeing
/// <c>ProductCreated</c>/<c>ProductPriceChanged</c>/<c>ProductUpdated</c>/<c>ProductDiscontinued</c>
/// are never published without that write having durably committed, and never lost if the write
/// commits but the immediate publish attempt fails.
///
/// Does NOT extend <see cref="Kart.Shared.Domain.OutboxEventBase"/>: that base is Guid-keyed
/// (<c>Id</c>, <c>AggregateId</c>) with no audit columns, while database-design.md's approved
/// schema for this table is a BIGSERIAL <c>id</c>, a <c>sku TEXT</c> correlation column (this
/// service's events are SKU-keyed, not Guid-aggregate-keyed - see <see cref="Variants.Variant"/>'s
/// own remarks), and <c>created_by</c>/<c>updated_by</c> audit columns (BRD §24.3). Forcing this
/// table into the shared base's shape would fight the approved DB design rather than serve it.
/// </summary>
public sealed class ProductOutboxEvent
{
    public long Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = "system:product-outbox-poller";

    /// <summary>
    /// The W3C <c>traceparent</c> of whatever request/activity caused this row to be written
    /// (captured from <c>Activity.Current</c> at INSERT time, while it's still the real inbound
    /// request's activity). The Outbox relay is a background poller running seconds later on its
    /// own, unrelated async context — without persisting this here, the trace this row's eventual
    /// publish belongs to would be un-recoverable, and a TraceId search in Grafana would show a
    /// gap between "row written" and "event published". Nullable because a row written outside
    /// any traced context (a migration/backfill script) legitimately has none.
    /// </summary>
    public string? TraceParent { get; private set; }

    /// <summary>EF Core materialization constructor.</summary>
    private ProductOutboxEvent()
    {
    }

    public static ProductOutboxEvent Create(string eventType, string sku, string payload, DateTimeOffset occurredAt, string createdBy, string? traceParent = null) =>
        new()
        {
            EventType = eventType,
            Sku = sku,
            Payload = payload,
            OccurredAt = occurredAt,
            CreatedBy = createdBy,
            TraceParent = traceParent,
        };

    /// <summary>Throws if already published - the poller only ever selects unpublished rows, so a
    /// second call always indicates a bug rather than a legitimate re-publish.</summary>
    public void MarkPublished(DateTimeOffset publishedAt, string updatedBy)
    {
        if (PublishedAt is not null)
        {
            throw new InvalidOperationException($"Outbox event {Id} was already published at {PublishedAt:O} and cannot be re-published.");
        }

        PublishedAt = publishedAt;
        UpdatedBy = updatedBy;
    }
}
