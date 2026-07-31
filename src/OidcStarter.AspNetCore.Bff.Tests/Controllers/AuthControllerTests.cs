using System.Security.Claims;
using System.Text.Json;
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

    [Theory]
    [InlineData("google")]
    [InlineData("GOOGLE")]
    public void Login_for_google_challenges_the_internal_google_scheme_with_a_canonical_marker(string provider)
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("oidc"));

        var result = Assert.IsType<ChallengeResult>(controller.Login(provider));

        Assert.Contains(OidcStarterGoogleDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Equal(
            "google",
            result.Properties?.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey]);
    }

    [Fact]
    public void Login_challenges_google_when_google_is_the_configured_default()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"));

        var result = Assert.IsType<ChallengeResult>(controller.Login());

        Assert.Contains(OidcStarterGoogleDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.Equal(
            "google",
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
    public void Providers_includes_google_metadata_without_exposing_its_scheme()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"));

        var result = controller.Providers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var providers = Assert.IsAssignableFrom<IReadOnlyList<LoginProviderResponse>>(okResult.Value);
        var google = Assert.Single(providers.Where(provider => provider.Id == "google"));
        Assert.Equal("Google", google.DisplayName);
        Assert.True(google.IsDefault);
        Assert.Equal("/api/auth/login/google", google.LoginUrl);
        Assert.DoesNotContain(
            typeof(LoginProviderResponse).GetProperties(),
            property => property.Name == "AuthenticationScheme");
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

    [Theory]
    [InlineData("oidc", "oidc")]
    [InlineData("OIDC", "oidc")]
    [InlineData("google", "google")]
    [InlineData("GOOGLE", "google")]
    public async Task Me_exposes_the_canonical_registered_provider_id_from_the_cookie_ticket(
        string marker,
        string expectedProviderId)
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("oidc"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties(marker));

        var result = await controller.Me();

        var response = GetCurrentUserResponse(result);
        Assert.Equal(expectedProviderId, response.ExternalIdentity?.ProviderId);
        Assert.Null(response.ExternalIdentity?.EmailVerified);
        Assert.Null(response.ExternalIdentity?.PictureUrl);
    }

    [Fact]
    public async Task Me_exposes_a_registered_custom_provider_id_from_the_cookie_ticket()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateCustomRegistry(),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("CUSTOM"));

        var result = await controller.Me();

        Assert.Equal("custom", GetCurrentUserResponse(result).ExternalIdentity?.ProviderId);
    }

    [Fact]
    public async Task Me_projects_normalized_external_identity_profile_claims_from_the_validated_cookie_ticket()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("google"));
        controller.HttpContext.User = CreateCookieTicketPrincipal(
            new Claim(ExternalIdentityClaimTypes.EmailVerified, "true", ClaimValueTypes.Boolean),
            new Claim(ExternalIdentityClaimTypes.PictureUrl, "https://example.test/picture.jpg"));

        var response = GetCurrentUserResponse(await controller.Me());

        Assert.Equal("google", response.ExternalIdentity?.ProviderId);
        Assert.True(response.ExternalIdentity?.EmailVerified);
        Assert.Equal("https://example.test/picture.jpg", response.ExternalIdentity?.PictureUrl);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("not-a-boolean", null)]
    public async Task Me_projects_only_valid_normalized_verified_email_values(string claimValue, bool? expectedValue)
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("google"));
        controller.HttpContext.User = CreateCookieTicketPrincipal(
            new Claim(ExternalIdentityClaimTypes.EmailVerified, claimValue));

        var response = GetCurrentUserResponse(await controller.Me());

        Assert.Equal("google", response.ExternalIdentity?.ProviderId);
        Assert.Equal(expectedValue, response.ExternalIdentity?.EmailVerified);
        Assert.Null(response.ExternalIdentity?.PictureUrl);
    }

    [Fact]
    public async Task Me_hides_blank_normalized_picture_claims_but_preserves_provider_provenance()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("google"));
        controller.HttpContext.User = CreateCookieTicketPrincipal(
            new Claim(ExternalIdentityClaimTypes.PictureUrl, "   "));

        var response = GetCurrentUserResponse(await controller.Me());

        Assert.Equal("google", response.ExternalIdentity?.ProviderId);
        Assert.Null(response.ExternalIdentity?.EmailVerified);
        Assert.Null(response.ExternalIdentity?.PictureUrl);
    }

    [Fact]
    public async Task Me_projects_normalized_external_identity_profile_claims_for_a_registered_custom_provider()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateCustomRegistry(),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("custom"));
        controller.HttpContext.User = CreateCookieTicketPrincipal(
            new Claim(ExternalIdentityClaimTypes.EmailVerified, "false"),
            new Claim(ExternalIdentityClaimTypes.PictureUrl, "https://example.test/custom-picture.jpg"));

        var response = GetCurrentUserResponse(await controller.Me());

        Assert.Equal("custom", response.ExternalIdentity?.ProviderId);
        Assert.False(response.ExternalIdentity?.EmailVerified);
        Assert.Equal("https://example.test/custom-picture.jpg", response.ExternalIdentity?.PictureUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("removed-provider")]
    public async Task Me_hides_missing_blank_and_unknown_provider_markers(string? marker)
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateRegistry("oidc"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties(marker));

        var result = await controller.Me();

        var response = GetCurrentUserResponse(result);
        Assert.Null(response.ExternalIdentity);

        if (marker == "removed-provider")
        {
            Assert.DoesNotContain(marker, JsonSerializer.Serialize(response));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("removed-provider")]
    public async Task Me_does_not_expose_normalized_profile_claims_without_valid_provider_provenance(string? marker)
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("oidc"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties(marker));
        controller.HttpContext.User = CreateCookieTicketPrincipal(
            new Claim(ExternalIdentityClaimTypes.EmailVerified, "true"),
            new Claim(ExternalIdentityClaimTypes.PictureUrl, "https://example.test/picture.jpg"));

        var response = GetCurrentUserResponse(await controller.Me());

        Assert.Null(response.ExternalIdentity);
        Assert.DoesNotContain("picture.jpg", JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task Me_does_not_fall_back_to_raw_provider_profile_claims()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            currentUserService: CreateAuthenticatedCurrentUserService(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("google"));
        controller.HttpContext.User = CreateCookieTicketPrincipal(
            new Claim("email_verified", "true"),
            new Claim("verified_email", "true"),
            new Claim("picture", "https://example.test/raw-picture.jpg"));

        var response = GetCurrentUserResponse(await controller.Me());

        Assert.Equal("google", response.ExternalIdentity?.ProviderId);
        Assert.Null(response.ExternalIdentity?.EmailVerified);
        Assert.Null(response.ExternalIdentity?.PictureUrl);
    }

    [Fact]
    public void Current_user_response_preserves_its_existing_constructor_and_has_nullable_external_identity()
    {
        var response = new CurrentUserResponse(true, "user-123", "Test User", "testuser", "test@example.local")
        {
            Roles = ["reader"]
        };

        Assert.True(response.IsAuthenticated);
        Assert.Equal("user-123", response.Sub);
        Assert.Equal("Test User", response.Name);
        Assert.Equal("testuser", response.Username);
        Assert.Equal("test@example.local", response.Email);
        Assert.Equal(["reader"], response.Roles);
        Assert.Null(response.ExternalIdentity);
    }

    [Fact]
    public void External_identity_response_preserves_its_constructor_and_serializes_optional_profile_fields()
    {
        var externalIdentity = new ExternalIdentityResponse("oidc");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            externalIdentity,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal(
            ["ProviderId", "EmailVerified", "PictureUrl"],
            typeof(ExternalIdentityResponse).GetProperties().Select(static property => property.Name));
        Assert.Null(externalIdentity.EmailVerified);
        Assert.Null(externalIdentity.PictureUrl);
        Assert.Equal("oidc", document.RootElement.GetProperty("providerId").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("emailVerified").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("pictureUrl").ValueKind);
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
    public void Logout_signs_out_only_the_cookie_for_a_google_provider_session()
    {
        var controller = CreateController(
            new FakeAntiforgery(),
            configureOptions: options => options.LoginProviders = CreateGoogleRegistry("google"),
            cookieAuthenticationProperties: CreateCookieAuthenticationProperties("google"));
        controller.HttpContext.Request.Headers.Origin = "http://localhost:4200";

        var result = Assert.IsType<SignOutResult>(controller.Logout());

        Assert.Equal([CookieAuthenticationDefaults.AuthenticationScheme], result.AuthenticationSchemes);
        Assert.DoesNotContain(OidcStarterGoogleDefaults.AuthenticationScheme, result.AuthenticationSchemes);
        Assert.DoesNotContain(OpenIdConnectDefaults.AuthenticationScheme, result.AuthenticationSchemes);
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

    private static LoginProviderRegistry CreateGoogleRegistry(string defaultProviderId)
        => new(
        [
            new LoginProviderDescriptor(
                "oidc",
                "OpenID Connect",
                OpenIdConnectDefaults.AuthenticationScheme,
                true),
            new LoginProviderDescriptor(
                OidcStarterGoogleDefaults.ProviderId,
                OidcStarterGoogleDefaults.DisplayName,
                OidcStarterGoogleDefaults.AuthenticationScheme)
        ],
        defaultProviderId);

    private static LoginProviderRegistry CreateCustomRegistry()
        => new(
        [
            new LoginProviderDescriptor(
                "oidc",
                "OpenID Connect",
                OpenIdConnectDefaults.AuthenticationScheme,
                true),
            new LoginProviderDescriptor("custom", "Custom", "custom-scheme")
        ],
        "oidc");

    private static ICurrentUserService CreateAuthenticatedCurrentUserService()
        => new StubCurrentUserService(static (_, _) => new CurrentUserResponse(
            true,
            "user-123",
            "Test User",
            "testuser",
            "test@example.local"));

    private static CurrentUserResponse GetCurrentUserResponse(ActionResult<CurrentUserResponse> result)
        => Assert.IsType<CurrentUserResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static AuthenticationProperties CreateCookieAuthenticationProperties(string? providerId = null)
    {
        var properties = new AuthenticationProperties();

        if (providerId is not null)
        {
            properties.Items[LoginProviderAuthenticationProperties.ProviderIdItemKey] = providerId;
        }

        return properties;
    }

    private static ClaimsPrincipal CreateCookieTicketPrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "cookie"));

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
