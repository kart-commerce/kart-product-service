using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Features.GetProduct;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.GetProduct;

public sealed class GetProductQueryHandlerTests
{
    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();

    private GetProductQueryHandler CreateHandler() => new(_readModelRepository.Object);

    [Fact]
    public async Task Handle_NotFound_Throws()
    {
        _readModelRepository.Setup(r => r.GetBySkuAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((ProductReadModel?)null);

        var act = () => CreateHandler().Handle(new GetProductQuery("missing"), CancellationToken.None);

        await act.Should().ThrowAsync<VariantNotFoundException>();
    }

    [Fact]
    public async Task Handle_Found_MapsReadModelToResponse()
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
    }
}
