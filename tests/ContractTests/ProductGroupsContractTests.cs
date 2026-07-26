using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Kart.Product.ContractTests;

/// <summary>HTTP wire-shape assertions (status codes, JSON field names, AdminOrPartner gating)
/// against contracts/api-contract.yaml - not domain-logic tests (those live in UnitTests).</summary>
public sealed class ProductGroupsContractTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
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
    public async Task Create_WithAdminScope_Returns201WithContractShape()
    {
        var client = CreateClient("admin");

        var response = await client.PostAsJsonAsync("/v1/product-groups", new
        {
            name = "Wireless Mouse",
            categoryId = "cat-1",
            sku = "sku-create-ok",
            price = new { amount = 24.99m, currency = "USD" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"productGroupId\"").And.Contain("\"sku\":\"sku-create-ok\"");
    }

    [Fact]
    public async Task Create_WithoutAdminOrPartnerScope_Returns403()
    {
        var client = CreateClient(); // no X-Test-Scope header at all

        var response = await client.PostAsJsonAsync("/v1/product-groups", new
        {
            name = "Wireless Mouse",
            categoryId = "cat-1",
            sku = "sku-forbidden",
            price = new { amount = 24.99m, currency = "USD" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WithDuplicateSku_Returns409WithProblemShape()
    {
        var client = CreateClient("partner");
        var body = new { name = "Wireless Mouse", categoryId = "cat-1", sku = "sku-dup", price = new { amount = 1m, currency = "USD" } };

        (await client.PostAsJsonAsync("/v1/product-groups", body)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync("/v1/product-groups", body);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await second.Content.ReadAsStringAsync();
        json.Should().Contain("\"errorCode\":\"SKU_ALREADY_EXISTS\"").And.Contain("\"traceId\"");
    }

    [Fact]
    public async Task AddVariant_ToUnknownProductGroup_Returns404()
    {
        var client = CreateClient("admin");

        var response = await client.PostAsJsonAsync($"/v1/product-groups/{Guid.NewGuid()}/variants", new
        {
            sku = "sku-orphan",
            price = new { amount = 1m, currency = "USD" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddVariant_ToExistingProductGroup_Returns201()
    {
        var client = CreateClient("admin");
        var createResponse = await client.PostAsJsonAsync("/v1/product-groups", new
        {
            name = "Wireless Mouse",
            categoryId = "cat-1",
            sku = "sku-parent-1",
            price = new { amount = 1m, currency = "USD" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElementResponse>();

        var response = await client.PostAsJsonAsync($"/v1/product-groups/{created!.ProductGroupId}/variants", new
        {
            sku = "sku-variant-2",
            price = new { amount = 2m, currency = "USD" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"sku\":\"sku-variant-2\"");
    }

    [Fact]
    public async Task UpdateProductGroup_ArchiveThenArchiveAgain_SecondCallReturns409()
    {
        var client = CreateClient("admin");
        var createResponse = await client.PostAsJsonAsync("/v1/product-groups", new
        {
            name = "Wireless Mouse",
            categoryId = "cat-1",
            sku = "sku-archive-1",
            price = new { amount = 1m, currency = "USD" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElementResponse>();

        var first = await client.PatchAsJsonAsync($"/v1/product-groups/{created!.ProductGroupId}", new { status = "Archived" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadAsStringAsync()).Should().Contain("\"affectedSkus\":[\"sku-archive-1\"]");

        var second = await client.PatchAsJsonAsync($"/v1/product-groups/{created.ProductGroupId}", new { status = "Archived" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateProductGroup_MixingFieldEditAndArchive_Returns409()
    {
        var client = CreateClient("admin");
        var createResponse = await client.PostAsJsonAsync("/v1/product-groups", new
        {
            name = "Wireless Mouse",
            categoryId = "cat-1",
            sku = "sku-mixed-1",
            price = new { amount = 1m, currency = "USD" },
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElementResponse>();

        var response = await client.PatchAsJsonAsync($"/v1/product-groups/{created!.ProductGroupId}", new { name = "New Name", status = "Archived" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record JsonElementResponse(Guid ProductGroupId, string Sku);
}
