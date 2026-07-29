using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
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
        Assert.Equal(
            "oidc",
            result.Properties?.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey]);
    }

    [Fact]
    public void Login_uses_root_fallback_redirect_when_frontend_origin_is_blank()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.FrontendOrigin = string.Empty);

        var result = Assert.IsType<ChallengeResult>(controller.Login());

        Assert.Equal("/", result.Properties?.RedirectUri);
    }

    [Fact]
    public void Login_challenges_the_configured_default_provider_scheme()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateRegistry("google"));

        var result = Assert.IsType<ChallengeResult>(controller.Login());

        Assert.Contains("google-scheme", result.AuthenticationSchemes);
        Assert.Equal(
            "google",
            result.Properties?.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey]);
    }

    [Theory]
    [InlineData("oidc")]
    [InlineData("OIDC")]
    public void Login_for_registered_provider_challenges_its_registered_authentication_scheme(string provider)
    {
        var controller = CreateController(new FakeAntiforgery());

        var result = Assert.IsType<ChallengeResult>(controller.Login(provider));

        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Equal("http://localhost:4200", result.Properties?.RedirectUri);
        Assert.Equal(
            "oidc",
            result.Properties?.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey]);
    }

    [Fact]
    public void Login_for_unknown_provider_returns_not_found_without_challenging_a_route_value()
    {
        var controller = CreateController(new FakeAntiforgery());

        var result = controller.Login("unknown-scheme");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Providers_returns_safe_canonical_login_provider_metadata()
    {
        var controller = CreateController(new FakeAntiforgery());

        var result = controller.Providers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<LoginProviderResponse>>(okResult.Value);
        var provider = Assert.Single(providers);
        Assert.Equal("oidc", provider.Id);
        Assert.Equal("OpenID Connect", provider.DisplayName);
        Assert.True(provider.IsDefault);
        Assert.Equal("/api/auth/login/oidc", provider.LoginUrl);
        Assert.Single(providers.Where(static provider => provider.IsDefault));
        Assert.DoesNotContain(
            typeof(LoginProviderResponse).GetProperties(),
            static property => property.Name == "AuthenticationScheme");
        Assert.DoesNotContain(
            typeof(LoginProviderResponse).GetProperties(),
            static property => property.Name == "SupportsRemoteSignOut");
    }

    [Fact]
    public void Providers_marks_only_the_configured_default_provider()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateRegistry("google"));

        var result = controller.Providers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<LoginProviderResponse>>(okResult.Value);
        var defaultProvider = Assert.Single(providers.Where(static provider => provider.IsDefault));
        Assert.Equal("google", defaultProvider.Id);
        Assert.Equal("/api/auth/login/google", defaultProvider.LoginUrl);
    }

    [Fact]
    public void AuthController_routes_match_current_api_auth_contract()
    {
        var controllerRoute = Assert.Single(
            typeof(AuthController).GetCustomAttributes(inherit: false).OfType<RouteAttribute>());

        Assert.Equal("api/auth", controllerRoute.Template);
        AssertHttpRoute<HttpGetAttribute>(nameof(AuthController.Login), "login");
        AssertHttpRoute<HttpGetAttribute>(nameof(AuthController.Login), "login/{provider}");
        AssertHttpRoute<HttpGetAttribute>(nameof(AuthController.Providers), "providers");
        AssertHttpRoute<HttpGetAttribute>(nameof(AuthController.Me), "me");
        AssertHttpRoute<HttpGetAttribute>(nameof(AuthController.Csrf), "csrf");
        AssertHttpRoute<HttpPostAttribute>(nameof(AuthController.Logout), "logout");
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
    public void Logout_signs_out_cookie_and_oidc_schemes_for_an_oidc_provider_session()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("oidc"));
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(controller.Logout());

        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Equal("http://localhost:4200", result.Properties?.RedirectUri);
    }

    [Fact]
    public void Logout_signs_out_only_the_cookie_for_a_local_only_provider_session()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateRegistry("google"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("google"));
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(controller.Logout());

        Assert.Equal([CookieAuthenticationDefaults.AuthenticationScheme], result.AuthenticationSchemes);
        Assert.DoesNotContain("google-scheme", result.AuthenticationSchemes);
        Assert.DoesNotContain(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Equal("http://localhost:4200", result.Properties?.RedirectUri);
    }

    [Fact]
    public void Logout_preserves_oidc_remote_sign_out_for_legacy_sessions_without_a_provider_marker()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties());
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(controller.Logout());

        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Contains(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
    }

    [Fact]
    public void Logout_signs_out_only_the_cookie_for_an_unknown_provider_marker()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("removed-provider"));
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(controller.Logout());

        Assert.Equal([CookieAuthenticationDefaults.AuthenticationScheme], result.AuthenticationSchemes);
    }

    [Fact]
    public void Logout_preserves_legacy_sign_out_behavior_when_no_cookie_ticket_is_available()
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
        Action<OidcStarterBffOptions>? configureOptions = null,
        AuthenticationProperties? cookieAuthenticationProperties = null)
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
            .AddSingleton<IAuthenticationService>(new FakeAuthenticationService(
                accessToken,
                cookieAuthenticationProperties))
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

    private static LoginProviderRegistry CreateRegistry(string defaultProviderId)
        => new(
        [
            new LoginProviderDescriptor(
                "oidc",
                "OpenID Connect",
                OpenIdConnectDefaults.AuthenticationScheme,
                true),
            new LoginProviderDescriptor("google", "Google", "google-scheme")
        ],
        defaultProviderId);

    private static AuthenticationProperties CreateCookieAuthenticationProperties(string? providerId = null)
    {
        var properties = new AuthenticationProperties();

        if (providerId is not null)
        {
            properties.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey] = providerId;
        }

        return properties;
    }

    private static void AssertHttpRoute<TAttribute>(string actionName, string expectedTemplate)
        where TAttribute : HttpMethodAttribute
    {
        var method = Assert.Single(typeof(AuthController).GetMethods().Where(method =>
            method.Name == actionName
            && method.GetCustomAttributes(inherit: false)
                .OfType<TAttribute>()
                .Any(attribute => attribute.Template == expectedTemplate)));

        Assert.Contains(
            method.GetCustomAttributes(inherit: false).OfType<TAttribute>(),
            attribute => attribute.Template == expectedTemplate);
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

    private sealed class FakeAuthenticationService(
        string? accessToken,
        AuthenticationProperties? cookieAuthenticationProperties) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            if (scheme == CookieAuthenticationDefaults.AuthenticationScheme
                && cookieAuthenticationProperties is not null)
            {
                var cookieTicket = new AuthenticationTicket(
                    context.User,
                    cookieAuthenticationProperties,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return Task.FromResult(AuthenticateResult.Success(cookieTicket));
            }

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
