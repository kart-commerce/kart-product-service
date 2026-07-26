using System.Text.Json;
using FluentAssertions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Features.ProjectCatalogEvent;
using Kart.Product.Domain.Events;
using Kart.Product.Domain.Variants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.ProjectCatalogEvent;

public sealed class ProjectCatalogEventCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();

    private ProjectCatalogEventCommandHandler CreateHandler() =>
        new(_readModelRepository.Object, NullLogger<ProjectCatalogEventCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ProductCreated_UpsertsFullDocument()
    {
        var evt = new ProductCreatedDomainEvent(
            "sku-1", Guid.NewGuid(), "Mouse", "desc", "cat-1", "Acme",
            new Money(9.99m, "USD"), "Active", ProductAttributes.Empty, Now);

        var command = new ProjectCatalogEventCommand("ProductCreated", JsonSerializer.Serialize(evt, JsonOptions));

        await CreateHandler().Handle(command, CancellationToken.None);

        _readModelRepository.Verify(r => r.UpsertAsync(It.Is<Kart.Product.Application.Common.Models.ProductReadModel>(m => m.Sku == "sku-1" && m.Name == "Mouse"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductPriceChanged_UpdatesOnlyPrice()
    {
        var evt = new ProductPriceChangedDomainEvent("sku-1", new Money(9.99m, "USD"), new Money(14.99m, "USD"), Now);
        var command = new ProjectCatalogEventCommand("ProductPriceChanged", JsonSerializer.Serialize(evt, JsonOptions));

        await CreateHandler().Handle(command, CancellationToken.None);

        _readModelRepository.Verify(r => r.UpdatePriceAsync("sku-1", 14.99m, "USD", Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductUpdated_SetsOnlyTheChangedFields()
    {
        var evt = new ProductUpdatedDomainEvent(
            "sku-1", ["name"], "New Name", "desc", "cat-1", "Acme", "Active", ProductAttributes.Empty, Now);

        var command = new ProjectCatalogEventCommand("ProductUpdated", JsonSerializer.Serialize(evt, JsonOptions));

        await CreateHandler().Handle(command, CancellationToken.None);

        _readModelRepository.Verify(r => r.UpdateFieldsAsync(
            "sku-1",
            It.Is<IReadOnlyDictionary<string, object?>>(fields => fields.Count == 1 && (string)fields["name"]! == "New Name"),
            Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ProductDiscontinued_MarksDiscontinued()
    {
        var evt = new ProductDiscontinuedDomainEvent("sku-1", Now);
        var command = new ProjectCatalogEventCommand("ProductDiscontinued", JsonSerializer.Serialize(evt, JsonOptions));

        await CreateHandler().Handle(command, CancellationToken.None);

        _readModelRepository.Verify(r => r.MarkDiscontinuedAsync("sku-1", Now, It.IsAny<CancellationToken>()), Times.Once);
    }
}
