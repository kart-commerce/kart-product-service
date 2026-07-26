namespace Kart.Product.Api.Controllers.Requests;

/// <summary>api-contract.yaml's <c>Money</c> schema, as received over the wire.</summary>
public sealed record MoneyRequest(decimal Amount, string Currency);
