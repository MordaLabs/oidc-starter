using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Authorization;

public static class OidcStarterAuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireOidcStarterScopes(
        this AuthorizationPolicyBuilder builder,
        params string[] scopes)
    {
        var requiredScopes = Normalize(scopes).ToArray();

        return builder.RequireAssertion(context =>
            requiredScopes.All(requiredScope => HasScope(context.User, requiredScope)));
    }

    public static AuthorizationPolicyBuilder RequireOidcStarterClaims(
        this AuthorizationPolicyBuilder builder,
        params RequiredClaimOptions[] claims)
    {
        var requiredClaims = claims
            .Where(static claim => !string.IsNullOrWhiteSpace(claim.Type))
            .ToArray();

        return builder.RequireAssertion(context =>
            requiredClaims.All(requiredClaim => HasClaim(context.User, requiredClaim)));
    }

    private static bool HasScope(ClaimsPrincipal user, string requiredScope)
        => user.FindAll("scope")
            .Concat(user.FindAll("scp"))
            .SelectMany(static claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));

    private static bool HasClaim(ClaimsPrincipal user, RequiredClaimOptions requiredClaim)
    {
        var values = Normalize(requiredClaim.Values).ToArray();
        var matchingClaims = user.FindAll(requiredClaim.Type).ToArray();

        if (matchingClaims.Length == 0)
        {
            return false;
        }

        return values.Length == 0
            || matchingClaims.Any(claim => values.Contains(claim.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Normalize(IEnumerable<string> values)
        => values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim());
}
