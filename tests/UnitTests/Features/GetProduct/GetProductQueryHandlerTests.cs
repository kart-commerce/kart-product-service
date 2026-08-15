using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Features.GetProduct;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.GetProduct;

public sealed class GetProductQueryHandlerTests
{
    private readonly Mock<IProductCache> _cache = new();
    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();

    private GetProductQueryHandler CreateHandler() => new(_cache.Object, _readModelRepository.Object, NullLogger<GetProductQueryHandler>.Instance);

    public GetProductQueryHandlerTests()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((ProductReadModel?)null);
    }

    [Fact]
    public async Task Handle_NotFound_Throws()
    {
        _readModelRepository.Setup(r => r.GetBySkuAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((ProductReadModel?)null);

        var act = () => CreateHandler().Handle(new GetProductQuery("missing"), CancellationToken.None);

        await act.Should().ThrowAsync<VariantNotFoundException>();
    }

    [Fact]
    public async Task Handle_CacheMiss_FallsBackToReadModelAndPopulatesCache()
    {
        var readModel = new ProductReadModel
        {
            Sku = "sku-1",
            ProductGroupId = Guid.NewGuid(),
            Name = "Mouse",
            Category = new ProductReadModelCategory("cat-1", null),
            PriceAmount = 24.99m,
            PriceCurrency = "USD",
            Status = "Active",
            RatingSummary = new ProductReadModelRatingSummary(4.5, 10),
            LastUpdatedAt = DateTimeOffset.UtcNow,
        };
        _readModelRepository.Setup(r => r.GetBySkuAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync(readModel);

        var response = await CreateHandler().Handle(new GetProductQuery("sku-1"), CancellationToken.None);

        response.Sku.Should().Be("sku-1");
        response.Price.Amount.Should().Be(24.99m);
        response.RatingSummary.Count.Should().Be(10);
        _cache.Verify(c => c.SetAsync(readModel, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CacheHit_NeverReadsTheReadModel()
    {
        var readModel = new ProductReadModel
        {
            Sku = "sku-1",
            ProductGroupId = Guid.NewGuid(),
            Name = "Mouse",
            Category = new ProductReadModelCategory("cat-1", null),
            PriceAmount = 24.99m,
            PriceCurrency = "USD",
            Status = "Active",
            RatingSummary = new ProductReadModelRatingSummary(4.5, 10),
            LastUpdatedAt = DateTimeOffset.UtcNow,
        };
        _cache.Setup(c => c.GetAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync(readModel);

        var response = await CreateHandler().Handle(new GetProductQuery("sku-1"), CancellationToken.None);

        response.Sku.Should().Be("sku-1");
        _readModelRepository.Verify(r => r.GetBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetAsync(It.IsAny<ProductReadModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
