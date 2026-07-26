namespace Kart.Product.Api.Security;

public static class AuthorizationPolicies
{
    /// <summary>ddd-model.md's <c>CanWrite</c> - Admin Service's or the Partner API's confirmed
    /// client-credentials service principal only (api-contract.yaml's <c>clientCredentials</c>
    /// security scheme, <c>admin</c>/<c>partner</c> scopes).</summary>
    public const string AdminOrPartner = "AdminOrPartner";
}
