using Kart.Product.Api.Controllers.Requests;
using Kart.Product.Api.Security;
using Kart.Product.Application.Features.AddVariant;
using Kart.Product.Application.Features.CreateProductGroup;
using Kart.Product.Application.Features.UpdateProductGroup;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kart.Product.Api.Controllers;

[ApiController]
[Route("v1/product-groups")]
[Authorize(Policy = AuthorizationPolicies.AdminOrPartner)]
public sealed class ProductGroupsController(ISender sender) : ControllerBase
{
    /// <summary>PRD-1: creates a Product (parent) and its initial Variant (SKU) together.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateProductGroupResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductGroupRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateProductGroupCommand(
            request.Name,
            request.Description,
            request.CategoryId,
            request.Brand,
            request.Sku,
            new Domain.Variants.Money(request.Price.Amount, request.Price.Currency),
            (request.Attributes ?? new ProductAttributesRequest(null, null, null)).ToDomain());

        var response = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(ProductsController.Get), "Products", new { sku = response.Sku }, response);
    }

    /// <summary>PRD-5: edits shared parent fields, or archives the entire product group.</summary>
    [HttpPatch("{productGroupId:guid}")]
    [ProducesResponseType(typeof(UpdateProductGroupResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid productGroupId, [FromBody] UpdateProductGroupRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateProductGroupCommand(productGroupId, request.Name, request.Description, request.CategoryId, request.Brand, request.Status);
        var response = await sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>PRD-2: adds a new sellable Variant (SKU) to an existing product group.</summary>
    [HttpPost("{productGroupId:guid}/variants")]
    [ProducesResponseType(typeof(AddVariantResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddVariant(Guid productGroupId, [FromBody] AddVariantRequest request, CancellationToken cancellationToken)
    {
        var command = new AddVariantCommand(
            productGroupId,
            request.Sku,
            new Domain.Variants.Money(request.Price.Amount, request.Price.Currency),
            (request.Attributes ?? new ProductAttributesRequest(null, null, null)).ToDomain());

        var response = await sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(ProductsController.Get), "Products", new { sku = response.Sku }, response);
    }
}
