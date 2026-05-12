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
        var properties = CreateFrontendRedirectProperties();

        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        var currentUser = currentUserService.GetCurrentUser(User, accessToken);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        return Ok(currentUser);
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
    public async Task<IActionResult> Logout()
    {
        if (!csrfOriginValidator.IsTrustedOrigin(Request))
        {
            return Forbid();
        }

        if (bffOptions.Value.RequireAntiforgeryToken)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return BadRequest();
            }
        }

        var properties = CreateFrontendRedirectProperties();

        return SignOut(
            properties,
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private AuthenticationProperties CreateFrontendRedirectProperties()
        => new()
        {
            RedirectUri = string.IsNullOrWhiteSpace(bffOptions.Value.FrontendOrigin)
                ? "/"
                : bffOptions.Value.FrontendOrigin
        };

    private bool ShouldSecureAntiforgeryRequestTokenCookie()
        => bffOptions.Value.AntiforgeryCookieSecurePolicy switch
        {
            CookieSecurePolicy.Always => true,
            CookieSecurePolicy.None => false,
            _ => Request.IsHttps
        };
}
