namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class OidcStarterFacebookDefaults
{
    public const string AuthenticationScheme = "OidcStarter.Facebook";
    public const string ProviderId = "facebook";
    public const string DisplayName = "Facebook";
    public const string DefaultCallbackPath = "/signin-facebook";
}
