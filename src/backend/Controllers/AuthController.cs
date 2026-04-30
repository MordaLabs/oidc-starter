using Backend.Configuration;
using Backend.Models.Auth;
using Backend.Security;
using Backend.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IOptions<StarterOptions> starterOptions,
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
            RedirectUri = string.IsNullOrWhiteSpace(starterOptions.Value.FrontendOrigin)
                ? "/"
                : starterOptions.Value.FrontendOrigin
        };
}
