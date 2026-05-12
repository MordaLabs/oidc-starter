using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using OidcStarter.AspNetCore.Bff.Authorization;

namespace Backend.Auth;

internal sealed class KeycloakRoleMapper : IOidcStarterRoleMapper
{
    // Sample-only Keycloak mapping. The reusable BFF package stays provider-agnostic.
    private static readonly HashSet<string> ExcludedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "offline_access",
        "uma_authorization"
    };

    public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
    {
        foreach (var role in GetRealmRoles(context))
        {
            if (ShouldIncludeRole(role))
            {
                yield return role;
            }
        }

        foreach (var role in GetClientRoles(context))
        {
            if (ShouldIncludeRole(role))
            {
                yield return role;
            }
        }
    }

    private static IEnumerable<string> GetRealmRoles(OidcStarterRoleMappingContext context)
    {
        foreach (var role in context.Principal.FindAll("realm_access.roles").Select(static claim => claim.Value))
        {
            yield return role;
        }

        foreach (var realmAccessClaim in context.Principal.FindAll("realm_access"))
        {
            using var document = ParseJsonClaim(realmAccessClaim);

            if (document is null
                || !document.RootElement.TryGetProperty("roles", out var rolesElement))
            {
                continue;
            }

            foreach (var role in ReadStringArray(rolesElement))
            {
                yield return role;
            }
        }

        using var tokenPayload = ParseJwtPayload(context.AccessToken);

        if (tokenPayload is not null
            && tokenPayload.RootElement.TryGetProperty("realm_access", out var realmAccess)
            && realmAccess.TryGetProperty("roles", out var tokenRoles))
        {
            foreach (var role in ReadStringArray(tokenRoles))
            {
                yield return role;
            }
        }
    }

    private static IEnumerable<string> GetClientRoles(OidcStarterRoleMappingContext context)
    {
        foreach (var role in context.Principal
            .Claims
            .Where(static claim => claim.Type.StartsWith("resource_access.", StringComparison.Ordinal)
                && claim.Type.EndsWith(".roles", StringComparison.Ordinal))
            .Select(static claim => claim.Value))
        {
            yield return role;
        }

        foreach (var resourceAccessClaim in context.Principal.FindAll("resource_access"))
        {
            using var document = ParseJsonClaim(resourceAccessClaim);

            if (document is null)
            {
                continue;
            }

            foreach (var clientElement in document.RootElement.EnumerateObject())
            {
                if (!clientElement.Value.TryGetProperty("roles", out var rolesElement))
                {
                    continue;
                }

                foreach (var role in ReadStringArray(rolesElement))
                {
                    yield return role;
                }
            }
        }

        using var tokenPayload = ParseJwtPayload(context.AccessToken);

        if (tokenPayload is not null
            && tokenPayload.RootElement.TryGetProperty("resource_access", out var resourceAccess))
        {
            foreach (var clientElement in resourceAccess.EnumerateObject())
            {
                if (!clientElement.Value.TryGetProperty("roles", out var rolesElement))
                {
                    continue;
                }

                foreach (var role in ReadStringArray(rolesElement))
                {
                    yield return role;
                }
            }
        }
    }

    private static JsonDocument? ParseJsonClaim(Claim claim)
    {
        try
        {
            return JsonDocument.Parse(claim.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonDocument? ParseJwtPayload(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var parts = accessToken.Split('.');

        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(Base64UrlTextEncoder.Decode(parts[1]));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }

    private static bool ShouldIncludeRole(string role)
        => !string.IsNullOrWhiteSpace(role)
            && !ExcludedRoles.Contains(role)
            && !role.StartsWith("default-roles-", StringComparison.OrdinalIgnoreCase);
}
