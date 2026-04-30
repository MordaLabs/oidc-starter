using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
    CsrfOriginValidator csrfOriginValidator) : ControllerBase
{
    [HttpGet("login")]
    public IActionResult Login()
    {
        var properties = CreateFrontendRedirectProperties();

        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me()
    {
        var currentUser = currentUserService.GetCurrentUser(User);

        if (currentUser is null)
        {
            return Unauthorized();
        }

        return Ok(currentUser);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        if (!csrfOriginValidator.IsTrustedOrigin(Request))
        {
            return Forbid();
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
}
