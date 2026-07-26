using FluentAssertions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Features.ApplyRatingProjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.ApplyRatingProjection;

public sealed class ApplyRatingProjectionCommandHandlerTests
{
    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();

    private ApplyRatingProjectionCommandHandler CreateHandler() =>
        new(_readModelRepository.Object, NullLogger<ApplyRatingProjectionCommandHandler>.Instance);

    private static ProductReadModel ReadModelWithRating(double avg, int count) => new()
    {
        Sku = "sku-1",
        ProductGroupId = Guid.NewGuid(),
        Name = "Mouse",
        Category = new ProductReadModelCategory("cat-1", null),
        PriceAmount = 1m,
        PriceCurrency = "USD",
        Status = "Active",
        RatingSummary = new ProductReadModelRatingSummary(avg, count),
        LastUpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Handle_NoExistingReadModel_IsANoOp()
    {
        _readModelRepository.Setup(r => r.GetBySkuAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync((ProductReadModel?)null);

        await CreateHandler().Handle(new ApplyRatingProjectionCommand("sku-1", 5, null, null), CancellationToken.None);

        _readModelRepository.Verify(r => r.UpdateRatingSummaryAsync(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReviewSubmitted_IncrementsCountAndRecomputesAverage()
    {
        _readModelRepository.Setup(r => r.GetBySkuAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync(ReadModelWithRating(4.0, 1));

        await CreateHandler().Handle(new ApplyRatingProjectionCommand("sku-1", 5.0, null, null), CancellationToken.None);

        // (4.0*1 + 5.0) / 2 = 4.5
        _readModelRepository.Verify(r => r.UpdateRatingSummaryAsync("sku-1", 4.5, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReviewUpdated_RecomputesAverageWithoutChangingCount()
    {
        _readModelRepository.Setup(r => r.GetBySkuAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync(ReadModelWithRating(4.5, 2));

        await CreateHandler().Handle(new ApplyRatingProjectionCommand("sku-1", null, 5.0, 3.0), CancellationToken.None);

        // (4.5*2 - 5.0 + 3.0) / 2 = 3.5
        _readModelRepository.Verify(r => r.UpdateRatingSummaryAsync("sku-1", 3.5, 2, It.IsAny<CancellationToken>()), Times.Once);
    }
}
