using FluentAssertions;
using Kart.Product.Application.Common.Exceptions;
using Kart.Product.Domain.ProductGroups;
using Kart.Product.Domain.Variants;
using Kart.Product.Infrastructure.Persistence;
using Kart.Product.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kart.Product.IntegrationTests;

/// <summary>
/// edge-cases.md "SKU uniqueness enforcement": <c>ExistsAsync</c> in
/// CreateProductGroupCommandHandler/AddVariantCommandHandler is only a pre-check and is itself
/// racy (TOCTOU) - two concurrent writers for the same caller-supplied sku can both pass it before
/// either commits. The real guarantee is the `variants` table's own primary key. This proves
/// EfUnitOfWork.SaveChangesAsync translates that DB-level race into the same
/// <see cref="SkuAlreadyExistsException"/> the pre-check throws, per api-contract.yaml's documented
/// 409 SKU_ALREADY_EXISTS - not an unhandled 500 for whichever writer loses the race.
/// </summary>
[Collection("Postgres")]
public sealed class EfUnitOfWorkRaceConditionTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task SaveChangesAsync_ConcurrentInsertOfSameSku_LoserThrowsSkuAlreadyExistsException()
    {
        const string sku = "sku-race-condition";
        var now = DateTimeOffset.UtcNow;

        // Two independent connections/contexts, each already past its own app-level ExistsAsync
        // check (neither has committed yet) - the exact race window edge-cases.md calls out.
        await using var contextA = fixture.CreateDbContext();
        await using var contextB = fixture.CreateDbContext();

        var productGroupA = ProductGroup.Create("Mouse A", null, "cat-1", null, "admin-1", now);
        new ProductGroupRepository(contextA).Add(productGroupA);
        new VariantRepository(contextA).Add(Variant.Create(sku, productGroupA.Id, new Money(1m, "USD"), ProductAttributes.Empty, "admin-1", now));

        var productGroupB = ProductGroup.Create("Mouse B", null, "cat-1", null, "admin-2", now);
        new ProductGroupRepository(contextB).Add(productGroupB);
        new VariantRepository(contextB).Add(Variant.Create(sku, productGroupB.Id, new Money(2m, "USD"), ProductAttributes.Empty, "admin-2", now));

        // The winner commits first, so the loser's insert hits the real "PK_variants" constraint,
        // not the app-level pre-check (which both writers already passed by construction above).
        await new EfUnitOfWork(contextA).SaveChangesAsync(CancellationToken.None);

        var loser = () => new EfUnitOfWork(contextB).SaveChangesAsync(CancellationToken.None);
        (await loser.Should().ThrowAsync<SkuAlreadyExistsException>())
            .Which.Sku.Should().Be(sku);

        await using var verificationContext = fixture.CreateDbContext();
        (await verificationContext.Variants.CountAsync(v => v.Sku == sku)).Should().Be(1);
    }
}
