using System.Security.Claims;

namespace OidcStarter.AspNetCore.Bff.Authorization;

public sealed class OidcStarterRoleMappingContext(
    ClaimsPrincipal principal,
    string? accessToken = null)
{
    public ClaimsPrincipal Principal { get; } = principal;

    public string? AccessToken { get; } = accessToken;
}
