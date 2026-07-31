using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class OidcStarterFacebookOptionsPostConfigure(
    IOptions<OidcStarterBffOptions> bffOptions) : IPostConfigureOptions<FacebookOptions>
{
    public void PostConfigure(string? name, FacebookOptions options)
    {
        if (!string.Equals(name, OidcStarterFacebookDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        if (!options.CallbackPath.HasValue)
        {
            options.CallbackPath = OidcStarterFacebookDefaults.DefaultCallbackPath;
        }

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CorrelationCookie.SameSite = bffOptions.Value.CookieSameSite;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    }
}
