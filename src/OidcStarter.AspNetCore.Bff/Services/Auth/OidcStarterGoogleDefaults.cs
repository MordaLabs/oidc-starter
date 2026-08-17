namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class OidcStarterGoogleDefaults
{
    public const string AuthenticationScheme = "OidcStarter.Google";
    public const string ProviderId = "google";
    public const string DisplayName = "Google";
    public const string DefaultCallbackPath = "/signin-google";
}
