using System.Text.Json;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Product.Application.Features.ProjectCatalogEvent;

public sealed class ProjectCatalogEventCommandHandler(
    IProductReadModelRepository readModelRepository,
    ILogger<ProjectCatalogEventCommandHandler> logger) : IRequestHandler<ProjectCatalogEventCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>changedFields names (ProductUpdated's payload) -> the product_read_model field
    /// path each one maps to. Parent-group edits (name/description/categoryId/brand) and Variant
    /// attribute edits (size/color/extendedAttributes) both flow through this same event type.</summary>
    private static readonly IReadOnlyDictionary<string, string> ChangedFieldPaths = new Dictionary<string, string>
    {
        ["name"] = "name",
        ["description"] = "description",
        ["categoryId"] = "category.id",
        ["brand"] = "brand",
        ["size"] = "size",
        ["color"] = "color",
        ["extendedAttributes"] = "extendedAttributes",
    };

    public async Task Handle(ProjectCatalogEventCommand request, CancellationToken cancellationToken)
    {
        switch (request.EventType)
        {
            case "ProductCreated":
                await ProjectCreatedAsync(request.PayloadJson, cancellationToken);
                break;
            case "ProductPriceChanged":
                await ProjectPriceChangedAsync(request.PayloadJson, cancellationToken);
                break;
            case "ProductUpdated":
                await ProjectUpdatedAsync(request.PayloadJson, cancellationToken);
                break;
            case "ProductDiscontinued":
                await ProjectDiscontinuedAsync(request.PayloadJson, cancellationToken);
                break;
            default:
                logger.LogWarning("Unrecognized catalog event type {EventType} - skipping projection", request.EventType);
                break;
        }
    }

    private async Task ProjectCreatedAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var evt = Deserialize<ProductCreatedDomainEvent>(payloadJson);

        var readModel = new ProductReadModel
        {
            Sku = evt.Sku,
            ProductGroupId = evt.ProductGroupId,
            Name = evt.Name,
            Description = evt.Description,
            Category = new ProductReadModelCategory(evt.CategoryId, null),
            Brand = evt.Brand,
            PriceAmount = evt.Price.Amount,
            PriceCurrency = evt.Price.Currency,
            Status = evt.Status,
            Size = evt.Attributes.Size,
            Color = evt.Attributes.Color,
            ExtendedAttributes = evt.Attributes.ExtendedAttributes,
            RatingSummary = new ProductReadModelRatingSummary(0, 0),
            LastUpdatedAt = evt.OccurredAt,
        };

        await readModelRepository.UpsertAsync(readModel, cancellationToken);
        logger.LogInformation("Stage {Stage}: sku {Sku} upserted into product_read_model", "ReadModelPersisted", evt.Sku);
    }

    private async Task ProjectPriceChangedAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var evt = Deserialize<ProductPriceChangedDomainEvent>(payloadJson);
        await readModelRepository.UpdatePriceAsync(evt.Sku, evt.NewPrice.Amount, evt.NewPrice.Currency, evt.OccurredAt, cancellationToken);
        logger.LogInformation("Stage {Stage}: sku {Sku} price field patched in product_read_model (CacheInvalidated follows via Redis write-through)", "ReadModelPersisted", evt.Sku);
    }

    private async Task ProjectUpdatedAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var evt = Deserialize<ProductUpdatedDomainEvent>(payloadJson);

        var fields = new Dictionary<string, object?>();
        foreach (var changedField in evt.ChangedFields)
        {
            if (!ChangedFieldPaths.TryGetValue(changedField, out var path))
            {
                logger.LogWarning("Unrecognized changedFields entry {Field} on ProductUpdated for {Sku} - skipping that field", changedField, evt.Sku);
                continue;
            }

            fields[path] = changedField switch
            {
                "name" => evt.Name,
                "description" => evt.Description,
                "categoryId" => evt.CategoryId,
                "brand" => evt.Brand,
                "size" => evt.Attributes.Size,
                "color" => evt.Attributes.Color,
                "extendedAttributes" => evt.Attributes.ExtendedAttributes,
                _ => null,
            };
        }

        if (fields.Count > 0)
        {
            await readModelRepository.UpdateFieldsAsync(evt.Sku, fields, evt.OccurredAt, cancellationToken);
            logger.LogInformation("Stage {Stage}: sku {Sku} fields {Fields} patched in product_read_model", "ReadModelPersisted", evt.Sku, fields.Keys);
        }
    }

    private async Task ProjectDiscontinuedAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var evt = Deserialize<ProductDiscontinuedDomainEvent>(payloadJson);
        await readModelRepository.MarkDiscontinuedAsync(evt.Sku, evt.OccurredAt, cancellationToken);
        logger.LogInformation("Stage {Stage}: sku {Sku} marked discontinued in product_read_model", "ReadModelPersisted", evt.Sku);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"{typeof(T).Name} payload deserialized to null.");
}
