using FluentAssertions;
using Kart.Product.Application.Common.Models;
using Kart.Product.Infrastructure.ReadModel;
using Kart.Product.IntegrationTests.Fixtures;
using Xunit;

namespace Kart.Product.IntegrationTests;

/// <summary>
/// database-design.md's field-scoped concurrency-control decision: every projector writes only
/// the fields its own event owns, so two different event types projecting the same SKU never
/// clobber each other's fields. Verified here against a real MongoDB (Testcontainers), not just
/// mocked in UnitTests.
/// </summary>
[Collection("Mongo")]
public sealed class MongoProjectionRepositoryTests(MongoContainerFixture fixture)
{
    private static ProductReadModel NewReadModel(string sku) => new()
    {
        Sku = sku,
        ProductGroupId = Guid.NewGuid(),
        Name = "Wireless Mouse",
        Description = "desc",
        Category = new ProductReadModelCategory("cat-1", null),
        Brand = "Acme",
        PriceAmount = 24.99m,
        PriceCurrency = "USD",
        Status = "Active",
        Size = "M",
        Color = "Black",
        ExtendedAttributes = new Dictionary<string, object?> { ["material"] = "plastic" },
        RatingSummary = new ProductReadModelRatingSummary(0, 0),
        LastUpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task UpsertThenGetBySku_RoundTripsTheFullDocument()
    {
        var repository = new MongoProductReadModelRepository(fixture.CreateDatabase());
        var readModel = NewReadModel("sku-mongo-roundtrip");

        await repository.UpsertAsync(readModel, CancellationToken.None);
        var fetched = await repository.GetBySkuAsync("sku-mongo-roundtrip", CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Wireless Mouse");
        fetched.Category.Id.Should().Be("cat-1");
        fetched.ExtendedAttributes.Should().ContainKey("material");
    }

    [Fact]
    public async Task UpdatePrice_TouchesOnlyPriceAndLastUpdatedAt()
    {
        var repository = new MongoProductReadModelRepository(fixture.CreateDatabase());
        var readModel = NewReadModel("sku-mongo-price");
        await repository.UpsertAsync(readModel, CancellationToken.None);

        var newLastUpdated = DateTimeOffset.UtcNow.AddMinutes(1);
        await repository.UpdatePriceAsync("sku-mongo-price", 39.99m, "USD", newLastUpdated, CancellationToken.None);

        var fetched = await repository.GetBySkuAsync("sku-mongo-price", CancellationToken.None);
        fetched!.PriceAmount.Should().Be(39.99m);
        fetched.Name.Should().Be("Wireless Mouse", "the price projector must never touch catalog fields");
        fetched.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateRatingSummary_NeverTouchesPriceOrStatus()
    {
        var repository = new MongoProductReadModelRepository(fixture.CreateDatabase());
        var readModel = NewReadModel("sku-mongo-rating");
        await repository.UpsertAsync(readModel, CancellationToken.None);

        await repository.UpdateRatingSummaryAsync("sku-mongo-rating", 4.5, 10, CancellationToken.None);

        var fetched = await repository.GetBySkuAsync("sku-mongo-rating", CancellationToken.None);
        fetched!.RatingSummary.Avg.Should().Be(4.5);
        fetched.RatingSummary.Count.Should().Be(10);
        fetched.PriceAmount.Should().Be(24.99m, "the rating projector must never touch price");
        fetched.Status.Should().Be("Active", "the rating projector must never touch status");
    }

    [Fact]
    public async Task MarkDiscontinued_TouchesOnlyStatusAndLastUpdatedAt()
    {
        var repository = new MongoProductReadModelRepository(fixture.CreateDatabase());
        var readModel = NewReadModel("sku-mongo-discontinue");
        await repository.UpsertAsync(readModel, CancellationToken.None);

        await repository.MarkDiscontinuedAsync("sku-mongo-discontinue", DateTimeOffset.UtcNow, CancellationToken.None);

        var fetched = await repository.GetBySkuAsync("sku-mongo-discontinue", CancellationToken.None);
        fetched!.Status.Should().Be("Discontinued");
        fetched.PriceAmount.Should().Be(24.99m);
    }

    [Fact]
    public async Task UpdateFields_SetsOnlyTheNamedFields()
    {
        var repository = new MongoProductReadModelRepository(fixture.CreateDatabase());
        var readModel = NewReadModel("sku-mongo-fields");
        await repository.UpsertAsync(readModel, CancellationToken.None);

        var fields = new Dictionary<string, object?> { ["name"] = "New Name" };
        await repository.UpdateFieldsAsync("sku-mongo-fields", fields, DateTimeOffset.UtcNow, CancellationToken.None);

        var fetched = await repository.GetBySkuAsync("sku-mongo-fields", CancellationToken.None);
        fetched!.Name.Should().Be("New Name");
        fetched.Description.Should().Be("desc", "only 'name' was named in changedFields");
        fetched.Brand.Should().Be("Acme");
    }
}
