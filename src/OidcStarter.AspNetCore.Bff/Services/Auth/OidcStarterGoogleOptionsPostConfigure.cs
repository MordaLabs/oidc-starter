using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class OidcStarterGoogleOptionsPostConfigure(
    IOptions<OidcStarterBffOptions> bffOptions) : IPostConfigureOptions<GoogleOptions>
{
    public void PostConfigure(string? name, GoogleOptions options)
    {
        if (!string.Equals(name, OidcStarterGoogleDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        if (!options.CallbackPath.HasValue)
        {
            options.CallbackPath = OidcStarterGoogleDefaults.DefaultCallbackPath;
        }

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CorrelationCookie.SameSite = bffOptions.Value.CookieSameSite;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    }
}

