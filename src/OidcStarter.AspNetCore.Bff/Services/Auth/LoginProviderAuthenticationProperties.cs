using Microsoft.AspNetCore.Authentication;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class LoginProviderAuthenticationProperties
{
    private const string ProviderIdItemKey = "OidcStarter.AspNetCore.Bff.LoginProviderId";

    public static void SetLoginProviderId(AuthenticationProperties properties, string providerId)
        => properties.Items[ProviderIdItemKey] = providerId;

    public static bool TryGetLoginProviderId(
        AuthenticationProperties properties,
        out string? providerId)
        => properties.Items.TryGetValue(ProviderIdItemKey, out providerId);
}
