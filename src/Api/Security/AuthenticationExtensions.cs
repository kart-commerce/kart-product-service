using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Product.Api.Security;

/// <summary>
/// api-contract.yaml's <c>clientCredentials</c> security scheme - Admin Service's and the Partner
/// API's client-credentials JWTs. Validated the same way every other kart-* service validates an
/// Identity-issued token (kart-admin-service's AuthenticationExtensions.cs is the reference):
/// RS256, signing key resolved from Identity's JWKS endpoint. This service previously validated
/// against a symmetric <c>Jwt:SigningKey</c> with issuer/audience checks enabled - Identity's
/// JwtAccessTokenGenerator signs RS256 and sets neither `iss` nor `aud` on any token it mints, so
/// that configuration rejected every real token Identity could ever issue (signature-algorithm
/// mismatch alone was fatal; the issuer/audience checks were a second, independent rejection).
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddProductAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<JwksSigningKeyResolver>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(SetJwtBearerOptions);

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver>((options, resolver) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Identity's JwtAccessTokenGenerator sets neither `iss` nor `aud` on the
                    // tokens it mints - validating either here would reject every real token.
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = resolver.ResolveSigningKeys,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AdminOrPartner, policy =>
                policy.RequireAssertion(context =>
                    // Identity's JwtAccessTokenGenerator emits one "scopes" claim per scope
                    // (Claim("scopes", scope) for each), not a single space-delimited value -
                    // "scope"/"scp" are also checked (split on space) for any other issuer that
                    // follows the more common single-claim OAuth2 convention.
                    context.User.FindAll("scopes")
                        .Concat(context.User.FindAll("scope"))
                        .Concat(context.User.FindAll("scp"))
                        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Any(scope => scope is "admin" or "partner")));

        return services;
    }

    private static void SetJwtBearerOptions(JwtBearerOptions options)
    {
        // IMPORTANT: Disable claim type mapping on the handler itself
        // This helps to keep JWT claim names (like "sub") unchanged instead of converting to long XML URIs
        // Like "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" instead of "sub"
        options.MapInboundClaims = false;

        // Optional: Add events for debugging
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // <--For debugging what is getting inside the claims in Authorize attribute
                Console.WriteLine("Token validated successfully");
                var claims = context?.Principal?.Claims.ToList();
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"OnChallenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    }
}
