namespace Kart.Product.Application.Common.Exceptions;

/// <summary>No such <c>productGroupId</c>. Maps to 404 (api-contract.yaml).</summary>
public sealed class ProductGroupNotFoundException(Guid productGroupId) : Exception($"Product group '{productGroupId}' was not found.")
{
    public Guid ProductGroupId { get; } = productGroupId;
}
