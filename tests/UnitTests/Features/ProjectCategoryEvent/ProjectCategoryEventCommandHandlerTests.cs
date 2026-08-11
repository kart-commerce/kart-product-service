using System.Text.Json;
using FluentAssertions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Features.ProjectCategoryEvent;
using Kart.Product.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.ProjectCategoryEvent;

public sealed class ProjectCategoryEventCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();

    private ProjectCategoryEventCommandHandler CreateHandler() =>
        new(_readModelRepository.Object, NullLogger<ProjectCategoryEventCommandHandler>.Instance);

    [Fact]
    public async Task Handle_CategoryUpdated_BulkUpdatesCategoryNameForTheCategoryId()
    {
        var evt = new CategoryUpdatedDomainEvent("cat-1", "Consumer Electronics", null, [], 0, "renamed", Now);
        var command = new ProjectCategoryEventCommand("CategoryUpdated", JsonSerializer.Serialize(evt, JsonOptions));
        _readModelRepository
            .Setup(r => r.UpdateCategoryNameForCategoryAsync("cat-1", "Consumer Electronics", Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        await CreateHandler().Handle(command, CancellationToken.None);

        _readModelRepository.Verify(r => r.UpdateCategoryNameForCategoryAsync("cat-1", "Consumer Electronics", Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnrecognizedEventType_SkipsWithoutCallingTheRepository()
    {
        var command = new ProjectCategoryEventCommand("SomethingElse", "{}");

        await CreateHandler().Handle(command, CancellationToken.None);

        _readModelRepository.Verify(
            r => r.UpdateCategoryNameForCategoryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
