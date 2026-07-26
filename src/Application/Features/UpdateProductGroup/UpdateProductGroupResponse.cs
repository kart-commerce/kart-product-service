namespace Kart.Product.Application.Features.UpdateProductGroup;

public sealed record UpdateProductGroupResponse(Guid ProductGroupId, IReadOnlyList<string> AffectedSkus);
