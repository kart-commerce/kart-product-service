using System.Text.Json;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Features.ProjectCategoryEvent;

/// <summary>
/// Category &amp; Attribute Management (Admin) flow's only projector so far: denormalizes
/// CategoryUpdated's <c>name</c> onto every product_read_model document sharing that categoryId
/// (bulk, not single-SKU - unlike every ProjectCatalogEvent projector, which is always one SKU).
/// </summary>
public sealed class ProjectCategoryEventCommandHandler(
    IProductReadModelRepository readModelRepository,
    ILogger<ProjectCategoryEventCommandHandler> logger) : IRequestHandler<ProjectCategoryEventCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task Handle(ProjectCategoryEventCommand request, CancellationToken cancellationToken)
    {
        switch (request.EventType)
        {
            case "CategoryUpdated":
                await ProjectCategoryUpdatedAsync(request.PayloadJson, cancellationToken);
                break;
            default:
                logger.LogWarning("Unrecognized category event type {EventType} - skipping projection", request.EventType);
                break;
        }
    }

    private async Task ProjectCategoryUpdatedAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var evt = Deserialize<CategoryUpdatedDomainEvent>(payloadJson);

        logger.LogInformation(
            "Stage {Stage}: CategoryUpdated processing started for category {CategoryId}",
            "CategoryProjectionProcessingStarted",
            evt.CategoryId);

        var matched = await readModelRepository.UpdateCategoryNameForCategoryAsync(evt.CategoryId, evt.Name, evt.OccurredAt, cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: {Matched} product_read_model document(s) had category.name set to {Name} for category {CategoryId}",
            "ProductReadModelBulkPersisted",
            matched,
            evt.Name,
            evt.CategoryId);

        // No per-SKU cache invalidation here, deliberately: IProductCache's only write-through
        // path is single-SKU (UpdatePriceAsync), and this projector doesn't know which SKUs it
        // just touched without a second query. A cached product's category.name can lag behind a
        // rename until its own TTL expires or its next cache-aside miss - a known, narrower
        // version of the read-model staleness this service's GetProductQueryHandler already
        // tolerates for other fields. Logged explicitly rather than silently glossed over.
        logger.LogInformation(
            "Stage {Stage}: no per-SKU Redis cache entries invalidated for category {CategoryId} - cache-aside entries will reflect the rename on their own next TTL-driven refresh",
            "ProductCatalogCacheInvalidationSkipped",
            evt.CategoryId);

        logger.LogInformation("Stage {Stage}: CategoryUpdated processing completed for category {CategoryId}", "CategoryProjectionCompletedSuccessfully", evt.CategoryId);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"{typeof(T).Name} payload deserialized to null.");
}
