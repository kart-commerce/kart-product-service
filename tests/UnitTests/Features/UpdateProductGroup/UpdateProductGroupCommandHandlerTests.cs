using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Application.Features.UpdateProductGroup;
using Kart.Product.Domain.Events;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Kart.Product.UnitTests.Features.UpdateProductGroup;

public sealed class UpdateProductGroupCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IProductGroupRepository> _productGroupRepository = new();
    private readonly Mock<IVariantRepository> _variantRepository = new();
    private readonly Mock<IOutboxEventWriter> _outboxEventWriter = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    private UpdateProductGroupCommandHandler CreateHandler() => new(
        _productGroupRepository.Object,
        _variantRepository.Object,
        _outboxEventWriter.Object,
        _unitOfWork.Object,
        _currentPrincipal.Object,
        TimeProvider.System,
        NullLogger<UpdateProductGroupCommandHandler>.Instance);

    public UpdateProductGroupCommandHandlerTests()
    {
        _currentPrincipal.Setup(p => p.ClientId).Returns("admin-1");
    }

    [Fact]
    public async Task Handle_FieldEditAndArchiveTogether_Throws()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var command = new UpdateProductGroupCommand(group.Id, "New Name", null, null, null, "Archived");

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<MixedUpdateRequestException>();
    }

    [Fact]
    public async Task Handle_Archive_CascadesOnlyToActiveSiblings()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        group.Publish("admin-1", Now);

        var activeVariant = Variant.Create("sku-active", group.Id, new Money(9.99m, "USD"), ProductAttributes.Empty, "admin-1", Now);
        var alreadyDiscontinued = Variant.Create("sku-gone", group.Id, new Money(9.99m, "USD"), ProductAttributes.Empty, "admin-1", Now);
        alreadyDiscontinued.Discontinue("admin-1", Now);

        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _variantRepository.Setup(r => r.GetByProductGroupIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeVariant, alreadyDiscontinued]);

        var command = new UpdateProductGroupCommand(group.Id, null, null, null, null, "Archived");

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.AffectedSkus.Should().BeEquivalentTo(["sku-active"]);
        group.Status.Should().Be(ProductGroupStatus.Archived);
        activeVariant.Status.Should().Be(VariantStatus.Discontinued);
        _outboxEventWriter.Verify(w => w.Enqueue("sku-active", It.IsAny<ProductDiscontinuedDomainEvent>(), "admin-1"), Times.Once);
        _outboxEventWriter.Verify(w => w.Enqueue("sku-gone", It.IsAny<ProductDiscontinuedDomainEvent>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ArchiveTwice_Throws()
    {
        var group = ProductGroup.Create("Mouse", null, "cat-1", null, "admin-1", Now);
        group.Publish("admin-1", Now);
        group.Archive("admin-1", Now);

        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        var command = new UpdateProductGroupCommand(group.Id, null, null, null, null, "Archived");

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<Kart.Product.Domain.Common.Exceptions.ProductGroupAlreadyArchivedException>();
    }

    [Fact]
    public async Task Handle_FieldEdit_FansOutProductUpdatedToActiveSiblingsOnly()
    {
        var group = ProductGroup.Create("Mouse", "desc", "cat-1", null, "admin-1", Now);
        group.Publish("admin-1", Now);

        var activeVariant = Variant.Create("sku-active", group.Id, new Money(9.99m, "USD"), ProductAttributes.Empty, "admin-1", Now);

        _productGroupRepository.Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _variantRepository.Setup(r => r.GetByProductGroupIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([activeVariant]);

        var command = new UpdateProductGroupCommand(group.Id, "New Name", null, null, null, null);

        var response = await CreateHandler().Handle(command, CancellationToken.None);

        response.AffectedSkus.Should().BeEquivalentTo(["sku-active"]);
        _outboxEventWriter.Verify(w => w.Enqueue("sku-active", It.Is<ProductUpdatedDomainEvent>(e => e.ChangedFields.Contains("name")), "admin-1"), Times.Once);
    }
}
