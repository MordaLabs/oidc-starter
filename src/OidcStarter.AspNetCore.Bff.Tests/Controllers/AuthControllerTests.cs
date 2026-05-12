using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Controllers;
using OidcStarter.AspNetCore.Bff.Models.Auth;
using OidcStarter.AspNetCore.Bff.Security;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Tests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task Logout_rejects_missing_trusted_origin_before_antiforgery_validation()
    {
        var antiforgery = new FakeAntiforgery();
        var controller = CreateController(antiforgery);

        var result = await controller.Logout();

        Assert.IsType<ForbidResult>(result);
        Assert.False(antiforgery.ValidateRequestCalled);
    }

    [Fact]
    public async Task Logout_returns_bad_request_when_required_antiforgery_validation_fails()
    {
        var controller = CreateController(new FakeAntiforgery(throwsOnValidate: true));
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = await controller.Logout();

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Logout_signs_out_cookie_and_oidc_schemes_when_origin_and_antiforgery_are_valid()
    {
        var controller = CreateController(new FakeAntiforgery());
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(await controller.Logout());

        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
    }

    private static AuthController CreateController(IAntiforgery antiforgery)
    {
        var options = Options.Create(new OidcStarterBffOptions
        {
            FrontendOrigin = "http://localhost:4200",
            RequireAntiforgeryToken = true
        });
        var controller = new AuthController(
            options,
            new StubCurrentUserService(),
            antiforgery,
            new CsrfOriginValidator(options));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private sealed class FakeAntiforgery(bool throwsOnValidate = false) : IAntiforgery
    {
        public bool ValidateRequestCalled { get; private set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
            => new("request-token", "cookie-token", "__RequestVerificationToken", "X-XSRF-TOKEN");

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext)
            => new("request-token", "cookie-token", "__RequestVerificationToken", "X-XSRF-TOKEN");

        public Task<bool> IsRequestValidAsync(HttpContext httpContext)
            => Task.FromResult(!throwsOnValidate);

        public Task ValidateRequestAsync(HttpContext httpContext)
        {
            ValidateRequestCalled = true;

            if (throwsOnValidate)
            {
                throw new AntiforgeryValidationException("Invalid antiforgery token.");
            }

            return Task.CompletedTask;
        }

        public void SetCookieTokenAndHeader(HttpContext httpContext)
        {
        }
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        public CurrentUserResponse? GetCurrentUser(ClaimsPrincipal user, string? accessToken = null)
            => null;
    }
}
