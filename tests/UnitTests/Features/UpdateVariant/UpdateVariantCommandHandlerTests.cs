using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Features.UpdateVariant;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.UpdateVariant;

public sealed class UpdateVariantCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IVariantRepository> _variantRepository = new();
    private readonly Mock<IProductGroupRepository> _productGroupRepository = new();
    private readonly Mock<IProductReadModelRepository> _readModelRepository = new();
    private readonly Mock<IProductCache> _cache = new();
    private readonly Mock<IOutboxEventWriter> _outboxEventWriter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private UpdateVariantCommandHandler CreateHandler() => new(
        _variantRepository.Object,
        _productGroupRepository.Object,
        _readModelRepository.Object,
        _cache.Object,
        _outboxEventWriter.Object,
        _unitOfWork.Object,
        _currentPrincipal.Object,
        TimeProvider.System);

    public UpdateVariantCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ClientId).Returns("admin-1");
        _readModelRepository.Setup(r => r.GetBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Kart.Product.Application.Common.Models.ProductReadModel?)null);
    }

    private void SetUpExistingVariant(Variant variant, ProductGroup group)
    {
        _variantRepository.Setup(r => r.GetBySkuAsync(variant.Sku, It.IsAny<CancellationToken>())).ReturnsAsync(variant);
        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
    }

    [Fact]
    public async Task Handle_MoreThanOneFieldProvided_Throws()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        var variant = Variant.Create("sku-1", group.Id, new Money(9.99m, "USD"), ProductAttributes.Empty, "admin-1", Now);
        SetUpExistingVariant(variant, group);

        var command = new UpdateVariantCommand("sku-1", new Money(19.99m, "USD"), "Discontinued", null);

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<MixedUpdateRequestException>();
    }

    [Fact]
    public async Task Handle_PriceChange_UpdatesVariantAndEnqueuesPriceChangedEvent()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        var variant = Variant.Create("sku-1", group.Id, new Money(9.99m, "USD"), ProductAttributes.Empty, "admin-1", Now);
        SetUpExistingVariant(variant, group);

        var command = new UpdateVariantCommand("sku-1", new Money(19.99m, "USD"), null, null);

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.Price.Amount.Should().Be(19.99m);
        variant.Price.Amount.Should().Be(19.99m);
        _outboxEventWriter.Verify(w => w.Enqueue("sku-1", It.IsAny<Kart.Product.Domain.Events.ProductPriceChangedDomainEvent>(), "admin-1"), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.UpdatePriceAsync("sku-1", 19.99m, "USD", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Discontinue_UpdatesStatusAndEnqueuesDiscontinuedEvent()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        var variant = Variant.Create("sku-1", group.Id, new Money(9.99m, "USD"), ProductAttributes.Empty, "admin-1", Now);
        SetUpExistingVariant(variant, group);

        var command = new UpdateVariantCommand("sku-1", null, "Discontinued", null);

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.Status.Should().Be("Discontinued");
        _outboxEventWriter.Verify(w => w.Enqueue("sku-1", It.IsAny<Kart.Product.Domain.Events.ProductDiscontinuedDomainEvent>(), "admin-1"), Times.Once);
        _cache.Verify(c => c.UpdatePriceAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never,
            "the write-through cache path is price-only (design-decisions.md) - a status change must never touch it");
    }

    [Fact]
    public async Task Handle_VariantNotFound_Throws()
    {
        _variantRepository.Setup(r => r.GetBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Variant?)null);

        var command = new UpdateVariantCommand("sku-missing", new Money(1m, "USD"), null, null);

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<VariantNotFoundException>();
    }
}
