using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Kart.Product.Application.Common.Models;
using Xunit;

namespace Kart.Product.ContractTests;

public sealed class ProductsContractTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
{
    private HttpClient CreateClient(string? scope = null)
    {
        var client = factory.CreateClient();
        if (scope is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Scope", scope);
        }

        return client;
    }

    [Fact]
    public async Task Get_UnknownSku_Returns404WithProblemShape()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/v1/products/sku-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"errorCode\":\"VARIANT_NOT_FOUND\"");
    }

    [Fact]
    public async Task Get_ExistingSku_Returns200WithContractShape_AndRequiresNoAuth()
    {
        factory.ReadModelRepository.Seed(new ProductReadModel
        {
            Sku = "sku-get-ok",
            ProductGroupId = Guid.NewGuid(),
            Name = "Wireless Mouse",
            Category = new ProductReadModelCategory("cat-1", null),
            PriceAmount = 24.99m,
            PriceCurrency = "USD",
            Status = "Active",
            RatingSummary = new ProductReadModelRatingSummary(4.5, 12),
            LastUpdatedAt = DateTimeOffset.UtcNow,
        });

        var client = CreateClient(); // no scope header at all - GET is unconditionally public

        var response = await client.GetAsync("/v1/products/sku-get-ok");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"sku\":\"sku-get-ok\"").And.Contain("\"ratingSummary\"");
    }

    [Fact]
    public async Task Update_WithoutAdminOrPartnerScope_Returns403()
    {
        var client = CreateClient();

        var response = await client.PatchAsJsonAsync("/v1/products/sku-any", new { price = new { amount = 1m, currency = "USD" } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_MixingPriceAndStatus_Returns409()
    {
        var client = CreateClient("admin");
        await CreateProductGroup(client, "sku-mixed-variant");

        var response = await client.PatchAsJsonAsync("/v1/products/sku-mixed-variant", new
        {
            price = new { amount = 1m, currency = "USD" },
            status = "Discontinued",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_PriceChange_Returns200WithContractShape()
    {
        var client = CreateClient("admin");
        await CreateProductGroup(client, "sku-price-variant");

        var response = await client.PatchAsJsonAsync("/v1/products/sku-price-variant", new { price = new { amount = 39.99m, currency = "USD" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"amount\":39.99");
    }

    [Fact]
    public async Task Update_UnknownSku_Returns404()
    {
        var client = CreateClient("admin");

        var response = await client.PatchAsJsonAsync("/v1/products/sku-does-not-exist", new { price = new { amount = 1m, currency = "USD" } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task CreateProductGroup(HttpClient client, string sku)
    {
        var response = await client.PostAsJsonAsync("/v1/product-groups", new
        {
            name = "Wireless Mouse",
            categoryId = "cat-1",
            sku,
            price = new { amount = 1m, currency = "USD" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
