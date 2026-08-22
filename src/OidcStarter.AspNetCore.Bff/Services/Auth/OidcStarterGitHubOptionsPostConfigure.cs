using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class OidcStarterGitHubOptionsPostConfigure(
    IOptions<OidcStarterBffOptions> bffOptions) : IPostConfigureOptions<GitHubAuthenticationOptions>
{
    public void PostConfigure(string? name, GitHubAuthenticationOptions options)
    {
        if (!string.Equals(name, OidcStarterGitHubDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        if (!options.CallbackPath.HasValue)
        {
            options.CallbackPath = OidcStarterGitHubDefaults.DefaultCallbackPath;
        }

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.UsePkce = true;
        options.SaveTokens = false;
        options.CorrelationCookie.SameSite = bffOptions.Value.CookieSameSite;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    }
}
