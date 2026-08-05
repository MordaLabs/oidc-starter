namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class OidcStarterGitHubDefaults
{
    public const string AuthenticationScheme = "OidcStarter.GitHub";
    public const string ProviderId = "github";
    public const string DisplayName = "GitHub";
    public const string DefaultCallbackPath = "/signin-github";
}
