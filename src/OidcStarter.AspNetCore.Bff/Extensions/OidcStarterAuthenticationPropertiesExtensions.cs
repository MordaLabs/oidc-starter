using Microsoft.AspNetCore.Authentication;
using OidcStarter.AspNetCore.Bff.Models.Auth;
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

    public static bool TryGetOidcStarterValidatedOidcSignInMetadata(
        this AuthenticationProperties properties,
        out OidcStarterValidatedOidcSignInMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return LoginProviderAuthenticationProperties.TryGetValidatedOidcSignInMetadata(properties, out metadata);
    }
}
