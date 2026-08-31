using Microsoft.AspNetCore.Authentication;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Extensions;

public static class OidcStarterAuthenticationPropertiesExtensions
{
    public static bool TryGetOidcStarterLoginProviderId(
        this AuthenticationProperties properties,
        out string? providerId)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return LoginProviderAuthenticationProperties.TryGetLoginProviderId(properties, out providerId);
    }
}
