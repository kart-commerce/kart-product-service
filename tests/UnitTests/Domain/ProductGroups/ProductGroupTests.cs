using FluentAssertions;
using Kart.Product.Domain.Common.Exceptions;
using Kart.Product.Domain.ProductGroups;
using Xunit;

namespace Kart.Product.UnitTests.Domain.ProductGroups;

public sealed class ProductGroupTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_StartsInDraftStatus()
    {
        var group = ProductGroup.Create("Wireless Mouse", "desc", "cat-1", "Acme", "admin-1", Now);

        group.Status.Should().Be(ProductGroupStatus.Draft);
        group.Name.Should().Be("Wireless Mouse");
        group.CreatedBy.Should().Be("admin-1");
        group.UpdatedBy.Should().Be("admin-1");
    }

    [Fact]
    public void Publish_TransitionsToPublished()
    {
        var group = ProductGroup.Create("Wireless Mouse", null, "cat-1", null, "admin-1", Now);

        group.Publish("admin-1", Now);

        group.Status.Should().Be(ProductGroupStatus.Published);
    }

    [Fact]
    public void UpdateFields_ReturnsOnlyTheFieldsThatActuallyChanged()
    {
        var group = ProductGroup.Create("Wireless Mouse", "desc", "cat-1", "Acme", "admin-1", Now);
        group.Publish("admin-1", Now);

        var changed = group.UpdateFields("New Name", "desc", "cat-1", "Acme", "admin-2", Now.AddMinutes(1));

        changed.Should().BeEquivalentTo(["name"]);
        group.Name.Should().Be("New Name");
        group.UpdatedBy.Should().Be("admin-2");
    }

    [Fact]
    public void UpdateFields_OnArchivedGroup_Throws()
    {
        var group = ProductGroup.Create("Wireless Mouse", null, "cat-1", null, "admin-1", Now);
        group.Publish("admin-1", Now);
        group.Archive("admin-1", Now);

        var act = () => group.UpdateFields("New Name", null, null, null, "admin-1", Now);

        act.Should().Throw<ProductGroupAlreadyArchivedException>();
    }

    [Fact]
    public void Archive_IsOneDirectional_SecondCallThrows()
    {
        var group = ProductGroup.Create("Wireless Mouse", null, "cat-1", null, "admin-1", Now);
        group.Publish("admin-1", Now);
        group.Archive("admin-1", Now);

        var act = () => group.Archive("admin-1", Now);

        act.Should().Throw<ProductGroupAlreadyArchivedException>();
    }
}
