using FluentAssertions;
using Kart.Product.Domain.Common.Exceptions;
using Kart.Product.Domain.Variants;
using Xunit;

namespace Kart.Product.UnitTests.Domain.Variants;

public sealed class VariantTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Money InitialPrice = new(24.99m, "USD");

    private static Variant CreateActive() =>
        Variant.Create("sku-1", Guid.NewGuid(), InitialPrice, ProductAttributes.Empty, "admin-1", Now);

    [Fact]
    public void Create_StartsActive()
    {
        var variant = CreateActive();

        variant.Status.Should().Be(VariantStatus.Active);
        variant.Price.Should().Be(InitialPrice);
    }

    [Fact]
    public void ChangePrice_ReturnsThePreviousPrice()
    {
        var variant = CreateActive();
        var newPrice = new Money(29.99m, "USD");

        var oldPrice = variant.ChangePrice(newPrice, "admin-1", Now.AddMinutes(1));

        oldPrice.Should().Be(InitialPrice);
        variant.Price.Should().Be(newPrice);
    }

    [Fact]
    public void Discontinue_IsOneDirectional_SecondCallThrows()
    {
        var variant = CreateActive();
        variant.Discontinue("admin-1", Now);

        var act = () => variant.Discontinue("admin-1", Now);

        act.Should().Throw<VariantAlreadyDiscontinuedException>();
    }

    [Fact]
    public void ChangePrice_OnDiscontinuedVariant_Throws()
    {
        var variant = CreateActive();
        variant.Discontinue("admin-1", Now);

        var act = () => variant.ChangePrice(new Money(1m, "USD"), "admin-1", Now);

        act.Should().Throw<VariantDiscontinuedException>();
    }

    [Fact]
    public void UpdateAttributes_OnDiscontinuedVariant_Throws()
    {
        var variant = CreateActive();
        variant.Discontinue("admin-1", Now);

        var act = () => variant.UpdateAttributes(new ProductAttributes("L", "Red", new Dictionary<string, object?>()), "admin-1", Now);

        act.Should().Throw<VariantDiscontinuedException>();
    }

    [Fact]
    public void UpdateAttributes_UpdatesSizeColorAndExtendedAttributes()
    {
        var variant = CreateActive();
        var attributes = new ProductAttributes("M", "Blue", new Dictionary<string, object?> { ["material"] = "plastic" });

        variant.UpdateAttributes(attributes, "admin-1", Now);

        variant.Size.Should().Be("M");
        variant.Color.Should().Be("Blue");
        variant.ExtendedAttributes.Should().ContainKey("material");
    }
}
