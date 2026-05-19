using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
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
    public void Logout_rejects_missing_trusted_origin()
    {
        var antiforgery = new FakeAntiforgery();
        var controller = CreateController(antiforgery);

        var result = controller.Logout();

        Assert.IsType<ForbidResult>(result);
        Assert.False(antiforgery.ValidateRequestCalled);
    }

    [Fact]
    public void Logout_is_protected_by_package_antiforgery_filter()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Logout));

        Assert.NotNull(method);
        Assert.Contains(
            method.GetCustomAttributes(inherit: false),
            static attribute => attribute is OidcStarterValidateAntiforgeryTokenAttribute);
    }

    [Fact]
    public async Task Package_antiforgery_filter_returns_bad_request_when_validation_fails()
    {
        var filter = new OidcStarterValidateAntiforgeryTokenFilter(
            new FakeAntiforgery(throwsOnValidate: true));
        var context = CreateAuthorizationFilterContext();

        await filter.OnAuthorizationAsync(context);

        Assert.IsType<BadRequestResult>(context.Result);
    }

    [Fact]
    public async Task Package_antiforgery_filter_allows_request_when_validation_succeeds()
    {
        var antiforgery = new FakeAntiforgery();
        var filter = new OidcStarterValidateAntiforgeryTokenFilter(antiforgery);
        var context = CreateAuthorizationFilterContext();

        await filter.OnAuthorizationAsync(context);

        Assert.True(antiforgery.ValidateRequestCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void Logout_signs_out_cookie_and_oidc_schemes_when_origin_is_valid()
    {
        var controller = CreateController(new FakeAntiforgery());
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(controller.Logout());

        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
    }

    [Fact]
    public void Csrf_sets_readable_request_token_cookie_with_configured_same_site()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            options => options.CookieSameSite = SameSiteMode.Strict);

        var result = controller.Csrf();

        Assert.IsType<NoContentResult>(result);
        var setCookie = Assert.Single(controller.HttpContext.Response.Headers.SetCookie);
        Assert.Contains("XSRF-TOKEN=request-token", setCookie);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static AuthController CreateController(
        IAntiforgery antiforgery,
        Action<OidcStarterBffOptions>? configureOptions = null)
    {
        var bffOptions = new OidcStarterBffOptions
        {
            FrontendOrigin = "http://localhost:4200"
        };
        configureOptions?.Invoke(bffOptions);
        var options = Options.Create(bffOptions);
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

    private static AuthorizationFilterContext CreateAuthorizationFilterContext()
        => new(
            new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new ActionDescriptor()),
            []);

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
