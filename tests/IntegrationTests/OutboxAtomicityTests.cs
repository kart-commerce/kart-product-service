using FluentAssertions;
using Kart.Product.Application.Features.CreateProductGroup;
using Kart.Product.Domain.Variants;
using Kart.Product.Infrastructure.Messaging;
using Kart.Product.Infrastructure.Persistence;
using Kart.Product.IntegrationTests.Fakes;
using Kart.Product.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kart.Product.IntegrationTests;

/// <summary>database-design.md's Transactional Outbox: every write inserts its Outbox row in the
/// same PostgreSQL transaction as the domain write. Mirrors kart-inventory-service/
/// kart-category-service's OutboxAtomicityTests/CategoryOutboxTests.</summary>
[Collection("Postgres")]
public sealed class OutboxAtomicityTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task CreateProductGroup_WritesProductGroupVariantAndOutboxRow_InOneTransaction()
    {
        const string sku = "sku-outbox-atomicity";

        await using var dbContext = fixture.CreateDbContext();
        var handler = new CreateProductGroupCommandHandler(
            new ProductGroupRepository(dbContext),
            new VariantRepository(dbContext),
            new OutboxEventWriter(dbContext),
            new EfUnitOfWork(dbContext),
            new FixedCurrentPrincipal("admin-1"),
            TimeProvider.System);

        var command = new CreateProductGroupCommand("Mouse", "desc", "cat-1", "Acme", sku, new Money(24.99m, "USD"), ProductAttributes.Empty);
        var response = await handler.Handle(command, CancellationToken.None);

        await using var verificationContext = fixture.CreateDbContext();
        var productGroup = await verificationContext.ProductGroups.AsNoTracking().SingleAsync(p => p.Id == response.ProductGroupId);
        var variant = await verificationContext.Variants.AsNoTracking().SingleAsync(v => v.Sku == sku);
        var outboxRow = await verificationContext.OutboxEvents.AsNoTracking().SingleAsync(e => e.EventType == "ProductCreated" && e.Sku == sku);

        productGroup.Should().NotBeNull();
        variant.ProductGroupId.Should().Be(productGroup.Id);
        outboxRow.Payload.Should().Contain(sku).And.Contain("Mouse");
        outboxRow.PublishedAt.Should().BeNull("the relay hasn't run yet - this row is only durably queued, not yet published");
    }

    [Fact]
    public async Task CreateProductGroup_DuplicateSku_WritesNothing()
    {
        const string sku = "sku-outbox-dup";

        await using (var dbContext = fixture.CreateDbContext())
        {
            var handler = new CreateProductGroupCommandHandler(
                new ProductGroupRepository(dbContext),
                new VariantRepository(dbContext),
                new OutboxEventWriter(dbContext),
                new EfUnitOfWork(dbContext),
                new FixedCurrentPrincipal("admin-1"),
                TimeProvider.System);

            await handler.Handle(new CreateProductGroupCommand("Mouse", null, "cat-1", null, sku, new Money(1m, "USD"), ProductAttributes.Empty), CancellationToken.None);
        }

        await using (var dbContext = fixture.CreateDbContext())
        {
            var handler = new CreateProductGroupCommandHandler(
                new ProductGroupRepository(dbContext),
                new VariantRepository(dbContext),
                new OutboxEventWriter(dbContext),
                new EfUnitOfWork(dbContext),
                new FixedCurrentPrincipal("admin-1"),
                TimeProvider.System);

            var act = () => handler.Handle(new CreateProductGroupCommand("Mouse 2", null, "cat-1", null, sku, new Money(2m, "USD"), ProductAttributes.Empty), CancellationToken.None);
            await act.Should().ThrowAsync<Kart.Product.Application.Common.Exceptions.SkuAlreadyExistsException>();
        }

        await using var verificationContext = fixture.CreateDbContext();
        (await verificationContext.Variants.CountAsync(v => v.Sku == sku)).Should().Be(1);
    }
}
