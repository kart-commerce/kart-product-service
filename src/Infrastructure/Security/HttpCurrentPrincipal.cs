using Kart.Product.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Kart.Product.Infrastructure.Security;

/// <summary>
/// ddd-model.md's <c>CanWrite</c> invariant, read off the client-credentials JWT (api-contract.yaml's
/// <c>clientCredentials</c> security scheme, <c>admin</c>/<c>partner</c> scopes) - a coarse role
/// check only, no ownership comparison.
/// </summary>
public sealed class HttpCurrentPrincipal(IHttpContextAccessor httpContextAccessor) : ICurrentPrincipal
{
    public string ClientId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("azp")?.Value
                ?? user?.FindFirst("client_id")?.Value
                ?? user?.FindFirst("sub")?.Value
                ?? "unknown-client";
        }
    }

    public bool IsAdminOrPartner
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return false;
            }

            var scopes = user.FindAll("scope")
                .Concat(user.FindAll("scp"))
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            return scopes.Any(scope => scope is "admin" or "partner");
        }
    }
}
