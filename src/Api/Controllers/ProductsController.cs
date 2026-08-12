using Kart.Product.Api.Controllers.Requests;
using Kart.Product.Api.Security;
using Kart.Product.Application.Common.Models;
using Kart.Product.Application.Features.GetProduct;
using Kart.Product.Application.Features.UpdateVariant;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kart.Product.Api.Controllers;

[ApiController]
[Route("v1/products")]
public sealed class ProductsController(ISender sender, ILogger<ProductsController> logger) : ControllerBase
{
    /// <summary>PRD-3: public catalog read, served from the MongoDB read model. CanRead is
    /// unconditional (BRD §24.1.2) - no [Authorize] on this action.</summary>
    [HttpGet("{sku}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string sku, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new GetProductQuery(sku), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// PRD-4: updates one Variant's price, status, or attributes - exactly one per call. This is
    /// the only path for a price change (ProductPriceChanged) or a direct status/attribute edit -
    /// kart-admin-service has no proxy for it yet (see this session's known-limitations note), so
    /// a caller here is either the Partner API or an operator invoking Product's own write API
    /// directly; either way it's still squarely this flow's concern (ProductPriceChanged is one
    /// of the four events product-service's own catalog-projection consumer and search-service's
    /// consumer both react to).
    /// </summary>
    [HttpPatch("{sku}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrPartner)]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string sku, [FromBody] UpdateVariantRequest request, CancellationToken cancellationToken)
    {
        using var flowScope = KartFlowContext.Push("ProductCatalogManagementAdmin");
        logger.LogInformation("Stage {Stage}: update variant {Sku} received", "ProductGroupsControllerReceived", sku);

        var command = new UpdateVariantCommand(
            sku,
            request.Price is null ? null : new Domain.Variants.Money(request.Price.Amount, request.Price.Currency),
            request.Status,
            request.Attributes?.ToDomain());

        var response = await sender.Send(command, cancellationToken);
        logger.LogInformation("Stage {Stage}: variant {Sku} updated", "AdminProductManagementProcessCompletedSuccessfully", sku);
        return Ok(response);
    }
}
