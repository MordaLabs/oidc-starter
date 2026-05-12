using System.Security.Claims;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Models.Auth;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class CurrentUserService(IOptions<OidcStarterBffOptions> bffOptions) : ICurrentUserService
{
    public CurrentUserResponse? GetCurrentUser(ClaimsPrincipal user)
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
            Roles = GetRoles(user)
        };
    }

    private static string? GetClaim(ClaimsPrincipal principal, params string[] claimTypes)
        => claimTypes
            .Where(static claimType => !string.IsNullOrWhiteSpace(claimType))
            .Select(principal.FindFirstValue)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private IReadOnlyCollection<string> GetRoles(ClaimsPrincipal principal)
    {
        var roleClaimTypes = new[]
            {
                bffOptions.Value.RoleClaimType
            }
            .Concat(bffOptions.Value.AdditionalRoleClaimTypes)
            .Where(static claimType => !string.IsNullOrWhiteSpace(claimType))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return roleClaimTypes
            .SelectMany(principal.FindAll)
            .Select(static claim => claim.Value)
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
