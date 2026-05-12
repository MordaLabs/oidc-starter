using System.Security.Claims;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Models.Auth;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class CurrentUserService(
    IOptions<OidcStarterBffOptions> bffOptions,
    IEnumerable<IOidcStarterRoleMapper> roleMappers) : ICurrentUserService
{
    public CurrentUserResponse? GetCurrentUser(ClaimsPrincipal user, string? accessToken = null)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return new CurrentUserResponse(
            true,
            GetClaim(user, ClaimTypes.NameIdentifier, "sub"),
            GetClaim(user, bffOptions.Value.NameClaimType, ClaimTypes.Name, "name"),
            GetClaim(user, "preferred_username"),
            GetClaim(user, ClaimTypes.Email, "email"))
        {
            Roles = GetRoles(user, accessToken)
        };
    }

    private static string? GetClaim(ClaimsPrincipal principal, params string[] claimTypes)
        => claimTypes
            .Where(static claimType => !string.IsNullOrWhiteSpace(claimType))
            .Select(principal.FindFirstValue)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private IReadOnlyCollection<string> GetRoles(ClaimsPrincipal principal, string? accessToken)
    {
        var mappingContext = new OidcStarterRoleMappingContext(principal, accessToken);

        return roleMappers
            .SelectMany(mapper => mapper.GetRoles(mappingContext))
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
