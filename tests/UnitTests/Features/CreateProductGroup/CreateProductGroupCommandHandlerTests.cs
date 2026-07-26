using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Features.CreateProductGroup;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Kart.Shared.Domain;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.CreateProductGroup;

public sealed class CreateProductGroupCommandHandlerTests
{
    private readonly Mock<IProductGroupRepository> _productGroupRepository = new();
    private readonly Mock<IVariantRepository> _variantRepository = new();
    private readonly Mock<IOutboxEventWriter> _outboxEventWriter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private CreateProductGroupCommandHandler CreateHandler() => new(
        _productGroupRepository.Object,
        _variantRepository.Object,
        _outboxEventWriter.Object,
        _unitOfWork.Object,
        _currentPrincipal.Object,
        TimeProvider.System);

    public CreateProductGroupCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ClientId).Returns("admin-1");
    }

    [Fact]
    public async Task Handle_SkuAlreadyExists_Throws()
    {
        _variantRepository.Setup(r => r.ExistsAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new CreateProductGroupCommand("Mouse", null, "cat-1", null, "sku-1", new Money(9.99m, "USD"), ProductAttributes.Empty);

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SkuAlreadyExistsException>();
        _productGroupRepository.Verify(r => r.Add(It.IsAny<ProductGroup>()), Times.Never);
    }

    [Fact]
    public async Task Handle_HappyPath_SavesAggregatesAndEnqueuesProductCreated()
    {
        _variantRepository.Setup(r => r.ExistsAsync("sku-1", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new CreateProductGroupCommand("Mouse", "desc", "cat-1", "Acme", "sku-1", new Money(9.99m, "USD"), ProductAttributes.Empty);

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.Sku.Should().Be("sku-1");
        _productGroupRepository.Verify(r => r.Add(It.Is<ProductGroup>(g => g.Status == ProductGroupStatus.Published)), Times.Once);
        _variantRepository.Verify(r => r.Add(It.Is<Variant>(v => v.Sku == "sku-1")), Times.Once);
        _outboxEventWriter.Verify(w => w.Enqueue("sku-1", It.IsAny<IDomainEvent>(), "admin-1"), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
