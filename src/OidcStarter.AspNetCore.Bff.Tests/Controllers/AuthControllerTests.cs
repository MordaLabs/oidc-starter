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
using Microsoft.Extensions.DependencyInjection;
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
    public void Login_challenges_openid_connect_with_frontend_redirect()
    {
        var controller = CreateController(new FakeAntiforgery());

        var result = Assert.IsType<ChallengeResult>(controller.Login());

        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Equal("http://localhost:4200", result.Properties?.RedirectUri);
    }

    [Fact]
    public async Task Me_returns_unauthorized_when_current_user_service_has_no_session()
    {
        var controller = CreateController(new FakeAntiforgery());

        var result = await controller.Me();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Me_returns_current_user_with_access_token_from_session()
    {
        var expectedUser = new CurrentUserResponse(
            true,
            "user-123",
            "Test User",
            "testuser",
            "test@example.local");
        ClaimsPrincipal? observedPrincipal = null;
        string? observedAccessToken = null;
        var currentUserService = new StubCurrentUserService((principal, accessToken) =>
        {
            observedPrincipal = principal;
            observedAccessToken = accessToken;
            return expectedUser;
        });
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: currentUserService,
            accessToken: "access-token");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-123")
        ], authenticationType: "cookie"));
        controller.HttpContext.User = principal;

        var result = await controller.Me();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expectedUser, okResult.Value);
        Assert.Same(principal, observedPrincipal);
        Assert.Equal("access-token", observedAccessToken);
    }

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
    public async Task Logout_request_filter_returns_bad_request_when_antiforgery_validation_fails()
    {
        var antiforgery = new FakeAntiforgery(throwsOnValidate: true);
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Logout));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAntiforgery>(antiforgery)
                .BuildServiceProvider()
        };
        var context = CreateAuthorizationFilterContext(httpContext);

        Assert.NotNull(method);
        var attribute = Assert.Single(
            method.GetCustomAttributes(inherit: false),
            static attribute => attribute is OidcStarterValidateAntiforgeryTokenAttribute);
        var filterFactory = Assert.IsAssignableFrom<IFilterFactory>(attribute);
        var filter = Assert.IsAssignableFrom<IAsyncAuthorizationFilter>(
            filterFactory.CreateInstance(httpContext.RequestServices));

        await filter.OnAuthorizationAsync(context);

        Assert.True(antiforgery.ValidateRequestCalled);
        Assert.IsType<BadRequestResult>(context.Result);
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
            configureOptions: options => options.CookieSameSite = SameSiteMode.Strict);

        var result = controller.Csrf();

        Assert.IsType<NoContentResult>(result);
        var setCookie = Assert.Single(controller.HttpContext.Response.Headers.SetCookie);
        Assert.Contains("XSRF-TOKEN=request-token", setCookie);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static AuthController CreateController(
        IAntiforgery antiforgery,
        ICurrentUserService? currentUserService = null,
        string? accessToken = null,
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
            currentUserService ?? new StubCurrentUserService(),
            antiforgery,
            new CsrfOriginValidator(options));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(new FakeAuthenticationService(accessToken))
            .BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static AuthorizationFilterContext CreateAuthorizationFilterContext(
        DefaultHttpContext? httpContext = null)
        => new(
            new ActionContext(
                httpContext ?? new DefaultHttpContext(),
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

    private sealed class FakeAuthenticationService(string? accessToken) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (accessToken is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var properties = new AuthenticationProperties();
            properties.StoreTokens(
            [
                new AuthenticationToken
                {
                    Name = "access_token",
                    Value = accessToken
                }
            ]);
            var ticket = new AuthenticationTicket(
                context.User,
                properties,
                scheme ?? CookieAuthenticationDefaults.AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class StubCurrentUserService(
        Func<ClaimsPrincipal, string?, CurrentUserResponse?>? getCurrentUser = null) : ICurrentUserService
    {
        public CurrentUserResponse? GetCurrentUser(ClaimsPrincipal user, string? accessToken = null)
            => getCurrentUser?.Invoke(user, accessToken);
    }
}
