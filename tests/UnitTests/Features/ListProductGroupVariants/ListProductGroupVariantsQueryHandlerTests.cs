using FluentAssertions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Features.ListProductGroupVariants;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.ListProductGroupVariants;

public sealed class ListProductGroupVariantsQueryHandlerTests
{
    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();

    private ListProductGroupVariantsQueryHandler CreateHandler() => new(_readModelRepository.Object);

    private static ProductReadModel MakeReadModel(string sku, Guid groupId, string color) => new()
    {
        Sku = sku,
        ProductGroupId = groupId,
        Name = "Aura Phone",
        Category = new ProductReadModelCategory("cat-1", "Electronics"),
        PriceAmount = 999m,
        PriceCurrency = "USD",
        Status = "Active",
        Color = color,
        RatingSummary = new ProductReadModelRatingSummary(4.5, 10),
        LastUpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Handle_ReturnsEverySiblingVariant_MappedToProductResponseDto()
    {
        var groupId = Guid.NewGuid();
        var siblings = new List<ProductReadModel>
        {
            MakeReadModel("sku-blk", groupId, "Black"),
            MakeReadModel("sku-slv", groupId, "Silver"),
        };
        _readModelRepository.Setup(r => r.ListByProductGroupIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(siblings);

        var response = await CreateHandler().Handle(new ListProductGroupVariantsQuery(groupId), CancellationToken.None);

        response.Should().HaveCount(2);
        response.Select(r => r.Sku).Should().BeEquivalentTo("sku-blk", "sku-slv");
        response.Should().OnlyContain(r => r.ProductGroupId == groupId);
    }

    [Fact]
    public async Task Handle_NoSiblings_ReturnsEmptyList()
    {
        _readModelRepository.Setup(r => r.ListByProductGroupIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductReadModel>());

        var response = await CreateHandler().Handle(new ListProductGroupVariantsQuery(Guid.NewGuid()), CancellationToken.None);

        response.Should().BeEmpty();
    }
}
