using Kart.Product.Api.Controllers.Requests;
using Kart.Product.Api.Security;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Features.AddVariant;
using Kart.Product.Application.Features.CreateProductGroup;
using Kart.Product.Application.Features.ListProductGroupVariants;
using Kart.Product.Application.Features.UpdateProductGroup;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kart.Product.Api.Controllers;

[ApiController]
[Route("v1/product-groups")]
[Authorize(Policy = AuthorizationPolicies.AdminOrPartner)]
public sealed class ProductGroupsController(ISender sender, ILogger<ProductGroupsController> logger) : ControllerBase
{
    /// <summary>PRD-1: creates a Product (parent) and its initial Variant (SKU) together.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateProductGroupResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductGroupRequest request, CancellationToken cancellationToken)
    {
        using var flowScope = KartFlowContext.Push("ProductCatalogManagementAdmin");
        logger.LogInformation("Stage {Stage}: create product-group received for sku {Sku}", "ProductGroupsControllerReceived", request.Sku);

        var command = new CreateProductGroupCommand(
            request.Name,
            request.Description,
            request.CategoryId,
            request.Brand,
            request.Sku,
            new Domain.Variants.Money(request.Price.Amount, request.Price.Currency),
            (request.Attributes ?? new ProductAttributesRequest(null, null, null)).ToDomain(),
            request.ImageUrl);

        var response = await sender.Send(command, cancellationToken);
        logger.LogInformation("Stage {Stage}: product-group {ProductGroupId} / sku {Sku} created", "AdminProductManagementProcessCompletedSuccessfully", response.ProductGroupId, response.Sku);
        return CreatedAtAction(nameof(ProductsController.Get), "Products", new { sku = response.Sku }, response);
    }

    /// <summary>PRD-5: edits shared parent fields, or archives the entire product group.</summary>
    [HttpPatch("{productGroupId:guid}")]
    [ProducesResponseType(typeof(UpdateProductGroupResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid productGroupId, [FromBody] UpdateProductGroupRequest request, CancellationToken cancellationToken)
    {
        using var flowScope = KartFlowContext.Push("ProductCatalogManagementAdmin");
        logger.LogInformation("Stage {Stage}: update product-group {ProductGroupId} received", "ProductGroupsControllerReceived", productGroupId);

        var command = new UpdateProductGroupCommand(productGroupId, request.Name, request.Description, request.CategoryId, request.Brand, request.Status, request.ImageUrl);
        var response = await sender.Send(command, cancellationToken);
        logger.LogInformation("Stage {Stage}: product-group {ProductGroupId} updated", "AdminProductManagementProcessCompletedSuccessfully", productGroupId);
        return Ok(response);
    }

    /// <summary>PRD-2: adds a new sellable Variant (SKU) to an existing product group.</summary>
    [HttpPost("{productGroupId:guid}/variants")]
    [ProducesResponseType(typeof(AddVariantResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddVariant(Guid productGroupId, [FromBody] AddVariantRequest request, CancellationToken cancellationToken)
    {
        using var flowScope = KartFlowContext.Push("ProductCatalogManagementAdmin");
        logger.LogInformation("Stage {Stage}: add variant {Sku} to product-group {ProductGroupId} received", "ProductGroupsControllerReceived", request.Sku, productGroupId);

        var command = new AddVariantCommand(
            productGroupId,
            request.Sku,
            new Domain.Variants.Money(request.Price.Amount, request.Price.Currency),
            (request.Attributes ?? new ProductAttributesRequest(null, null, null)).ToDomain());

        var response = await sender.Send(command, cancellationToken);
        logger.LogInformation("Stage {Stage}: variant {Sku} added to product-group {ProductGroupId}", "AdminProductManagementProcessCompletedSuccessfully", response.Sku, productGroupId);
        return CreatedAtAction(nameof(ProductsController.Get), "Products", new { sku = response.Sku }, response);
    }

    /// <summary>
    /// Public PDP variant-axis read (Normal Shopping &amp; Purchase Journey flow's "Select Variant"
    /// stage) — every sibling SKU sharing this product group. [AllowAnonymous] overrides this
    /// controller's class-level AdminOrPartner policy for this action only; every other action here
    /// stays admin/partner-gated.
    /// </summary>
    [HttpGet("{productGroupId:guid}/variants")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListVariants(Guid productGroupId, CancellationToken cancellationToken)
    {
        using var flowScope = KartFlowContext.Push("NormalShoppingPurchaseJourney");
        logger.LogInformation("Stage {Stage}: product-group variants requested for {ProductGroupId}", "ProductGroupVariantsRequested", productGroupId);

        var query = new ListProductGroupVariantsQuery(productGroupId);
        var response = await sender.Send(query, cancellationToken);

        logger.LogInformation("Stage {Stage}: {Count} variant(s) returned for product-group {ProductGroupId}", "ProductGroupVariantsReturned", response.Count, productGroupId);
        return Ok(response);
    }
}
