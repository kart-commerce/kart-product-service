using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Features.AddVariant;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.AddVariant;

public sealed class AddVariantCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IProductGroupRepository> _productGroupRepository = new();
    private readonly Mock<IVariantRepository> _variantRepository = new();
    private readonly Mock<IOutboxEventWriter> _outboxEventWriter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private AddVariantCommandHandler CreateHandler() => new(
        _productGroupRepository.Object,
        _variantRepository.Object,
        _outboxEventWriter.Object,
        _unitOfWork.Object,
        _currentPrincipal.Object,
        TimeProvider.System,
        NullLogger<AddVariantCommandHandler>.Instance);

    public AddVariantCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ClientId).Returns("admin-1");
    }

    [Fact]
    public async Task Handle_ProductGroupNotFound_Throws()
    {
        _productGroupRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ProductGroup?)null);

        var command = new AddVariantCommand(Guid.NewGuid(), "sku-2", new Money(1m, "USD"), ProductAttributes.Empty);

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ProductGroupNotFoundException>();
    }

    [Fact]
    public async Task Handle_SkuAlreadyExists_Throws()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _variantRepository.Setup(r => r.ExistsAsync("sku-2", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new AddVariantCommand(group.Id, "sku-2", new Money(1m, "USD"), ProductAttributes.Empty);

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SkuAlreadyExistsException>();
    }

    [Fact]
    public async Task Handle_HappyPath_AddsVariantAndEnqueuesProductCreated()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _variantRepository.Setup(r => r.ExistsAsync("sku-2", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new AddVariantCommand(group.Id, "sku-2", new Money(1m, "USD"), ProductAttributes.Empty);

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.Sku.Should().Be("sku-2");
        _variantRepository.Verify(r => r.Add(It.Is<Variant>(v => v.Sku == "sku-2" && v.ProductGroupId == group.Id)), Times.Once);
        _outboxEventWriter.Verify(w => w.Enqueue("sku-2", It.IsAny<Kart.Product.Domain.Events.ProductCreatedDomainEvent>(), "admin-1"), Times.Once);
    }
}
