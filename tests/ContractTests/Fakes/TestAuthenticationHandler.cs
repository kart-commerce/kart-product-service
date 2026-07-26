using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kart.Product.ContractTests.Fakes;

/// <summary>
/// Replaces real JWT validation in the contract-test host - always authenticates successfully,
/// with a `scope` claim driven by a test-only `X-Test-Scope` header (space separated), so tests
/// can exercise the AdminOrPartner policy without a real signed token. Mirrors
/// kart-inventory-service/kart-category-service's ContractTests convention.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    private const string ScopeHeader = "X-Test-Scope";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "test-principal"), new("azp", "test-client") };

        if (Request.Headers.TryGetValue(ScopeHeader, out var scopeHeader))
        {
            claims.Add(new Claim("scope", scopeHeader.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
