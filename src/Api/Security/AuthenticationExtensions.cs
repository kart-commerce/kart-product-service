using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Product.Api.Security;

/// <summary>
/// api-contract.yaml's <c>clientCredentials</c> security scheme - Admin Service's and the Partner
/// API's client-credentials JWTs, validated against a symmetric signing key for local/dev use
/// (production would point <c>Jwt:Authority</c> at kart-identity-service's JWKS endpoint instead -
/// out of scope for this service to stand up).
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddProductAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                };
                
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
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.AdminOrPartner, policy =>
                policy.RequireAssertion(context =>
                    context.User.FindAll("scope").Concat(context.User.FindAll("scp"))
                        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Any(scope => scope is "admin" or "partner")));

        return services;
    }
}
