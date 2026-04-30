using System.Security.Claims;
using OidcStarter.AspNetCore.Bff.Models.Auth;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class CurrentUserService : ICurrentUserService
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
            GetClaim(user, ClaimTypes.Name, "name"),
            GetClaim(user, "preferred_username"),
            GetClaim(user, ClaimTypes.Email, "email"));
    }

    private static string? GetClaim(ClaimsPrincipal principal, params string[] claimTypes)
        => claimTypes
            .Select(principal.FindFirstValue)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
}
