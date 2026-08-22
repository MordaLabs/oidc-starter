using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Models.Auth;
using OidcStarter.AspNetCore.Bff.Security;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IOptions<OidcStarterBffOptions> bffOptions,
    ICurrentUserService currentUserService,
    IAntiforgery antiforgery,
    CsrfOriginValidator csrfOriginValidator) : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login()
    {
        var loginProvider = bffOptions.Value.LoginProviders.DefaultProvider;
        var properties = CreateLoginProperties(loginProvider);

        return Challenge(properties, loginProvider.AuthenticationScheme);
    }

    [HttpGet("login/{provider}")]
    public IActionResult Login(string provider)
    {
        if (!bffOptions.Value.LoginProviders.TryGetProvider(provider, out var loginProvider))
        {
            return NotFound();
        }

        var properties = CreateLoginProperties(loginProvider);

        return Challenge(properties, loginProvider.AuthenticationScheme);
    }

    [HttpGet("providers")]
    public ActionResult<IReadOnlyList<LoginProviderResponse>> Providers()
        => Ok(bffOptions.Value.LoginProviders.Providers
            .Select(provider => new LoginProviderResponse(
                provider.Id,
                provider.DisplayName,
                provider.Id == bffOptions.Value.LoginProviders.DefaultProvider.Id,
                $"/api/auth/login/{provider.Id}"))
            .ToArray());

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        var currentUser = currentUserService.GetCurrentUser(User, accessToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        var externalIdentity = await GetExternalIdentityAsync();
        return Ok(externalIdentity is null
            ? currentUser
            : currentUser with { ExternalIdentity = externalIdentity });
    }

    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        Response.Cookies.Append(
            bffOptions.Value.AntiforgeryRequestTokenCookieName,
            tokens.RequestToken ?? string.Empty,
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                Path = "/",
                SameSite = bffOptions.Value.CookieSameSite,
                Secure = ShouldSecureAntiforgeryRequestTokenCookie()
            });

        return NoContent();
    }

    [HttpPost("logout")]
    [OidcStarterValidateAntiforgeryToken]
    public IActionResult Logout()
    {
        if (!csrfOriginValidator.IsTrustedOrigin(Request))
        {
            return Forbid();
        }

        var properties = CreateFrontendRedirectProperties();
        var authenticationResult = HttpContext
            .AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .GetAwaiter()
            .GetResult();
        var signOutSchemes = new List<string>
        {
            CookieAuthenticationDefaults.AuthenticationScheme
        };

        var remoteSignOutScheme = GetRemoteSignOutScheme(authenticationResult);
        if (remoteSignOutScheme is not null)
        {
            signOutSchemes.Add(remoteSignOutScheme);
        }

        return SignOut(properties, signOutSchemes.ToArray());
    }

    private AuthenticationProperties CreateLoginProperties(LoginProviderDescriptor loginProvider)
    {
        var properties = CreateFrontendRedirectProperties();
        properties.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey] = loginProvider.Id;

        return properties;
    }

    private AuthenticationProperties CreateFrontendRedirectProperties()
        => new()
        {
            RedirectUri = string.IsNullOrWhiteSpace(bffOptions.Value.FrontendOrigin)
                ? "/"
                : bffOptions.Value.FrontendOrigin
        };

    private string? GetRemoteSignOutScheme(AuthenticateResult authenticationResult)
    {
        if (!authenticationResult.Succeeded
            || authenticationResult.Properties is null
            || !authenticationResult.Properties.Items.TryGetValue(
                LoginProviderAuthenticationProperties.ProviderIdItemKey,
                out var providerId))
        {
            return OpenIdConnectDefaults.AuthenticationScheme;
        }

        return bffOptions.Value.LoginProviders.TryGetProvider(providerId, out var loginProvider)
            && loginProvider.SupportsRemoteSignOut
            ? loginProvider.AuthenticationScheme
            : null;
    }

    private async Task<ExternalIdentityResponse?> GetExternalIdentityAsync()
    {
        var authenticationResult = await HttpContext.AuthenticateAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authenticationResult.Succeeded
            || authenticationResult.Properties is null
            || !authenticationResult.Properties.Items.TryGetValue(
                LoginProviderAuthenticationProperties.ProviderIdItemKey,
                out var providerId)
            || !bffOptions.Value.LoginProviders.TryGetProvider(providerId, out var loginProvider))
        {
            return null;
        }

        return new ExternalIdentityResponse(loginProvider.Id)
        {
            EmailVerified = GetEmailVerified(authenticationResult.Principal),
            PictureUrl = GetPictureUrl(authenticationResult.Principal)
        };
    }

    private static bool? GetEmailVerified(System.Security.Claims.ClaimsPrincipal? principal)
        => bool.TryParse(
            principal?.FindFirst(ExternalIdentityClaimTypes.EmailVerified)?.Value,
            out var emailVerified)
            ? emailVerified
            : null;

    private static string? GetPictureUrl(System.Security.Claims.ClaimsPrincipal? principal)
    {
        var pictureUrl = principal?.FindFirst(ExternalIdentityClaimTypes.PictureUrl)?.Value;

        return string.IsNullOrWhiteSpace(pictureUrl)
            ? null
            : pictureUrl;
    }

    private bool ShouldSecureAntiforgeryRequestTokenCookie()
        => bffOptions.Value.AntiforgeryCookieSecurePolicy switch
        {
            CookieSecurePolicy.Always => true,
            CookieSecurePolicy.None => false,
            _ => Request.IsHttps
        };
}
