using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace OidcStarter.AspNetCore.Bff.Tests.Extensions;

public sealed class OidcStarterBffServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddOidcStarterBff_registers_authorization_policies_from_configuration()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["Starter:RequiredScopes:0"] = "profile",
            ["Starter:RequiredClaims:0:Type"] = "tenant",
            ["Starter:RequiredClaims:0:Values:0"] = "academy"
        });
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var authenticatedPolicy = await policyProvider.GetPolicyAsync(OidcStarterBffPolicies.AuthenticatedUser);
        var scopePolicy = await policyProvider.GetPolicyAsync(OidcStarterBffPolicies.ConfiguredRequiredScopes);
        var claimPolicy = await policyProvider.GetPolicyAsync(OidcStarterBffPolicies.ConfiguredRequiredClaims);

        Assert.NotNull(authenticatedPolicy);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authenticatedPolicy.AuthenticationSchemes);
        Assert.NotNull(scopePolicy);
        Assert.NotNull(claimPolicy);
    }

    [Fact]
    public void AddOidcStarterRoleMapper_composes_custom_mapper_with_default_mapper()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterRoleMapper<CustomRoleMapper>();
        services.AddOidcStarterBff(CreateConfiguration([]));

        using var provider = services.BuildServiceProvider();

        var mappers = provider.GetServices<IOidcStarterRoleMapper>().ToArray();
        Assert.Contains(mappers, static mapper => mapper is CustomRoleMapper);
        Assert.Contains(mappers, static mapper => mapper.GetType().Name == "DefaultOidcStarterRoleMapper");
    }

    [Fact]
    public void AddOidcStarterBff_applies_configured_same_site_to_cookie_related_options()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["Starter:CookieSameSite"] = "Strict",
            ["Oidc:Authority"] = "https://identity.example.test",
            ["Oidc:ClientId"] = "bff-client",
            ["Oidc:ClientSecret"] = "secret"
        });

        var cookieOptions = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var oidcOptions = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var antiforgeryOptions = provider
            .GetRequiredService<IOptions<AntiforgeryOptions>>()
            .Value;

        Assert.Equal(SameSiteMode.Strict, cookieOptions.Cookie.SameSite);
        Assert.Equal(SameSiteMode.Strict, oidcOptions.CorrelationCookie.SameSite);
        Assert.Equal(SameSiteMode.Strict, oidcOptions.NonceCookie.SameSite);
        Assert.Equal(SameSiteMode.Strict, antiforgeryOptions.Cookie.SameSite);
    }

    [Fact]
    public async Task AddOidcStarterBff_returns_unauthorized_for_api_login_redirect()
    {
        using var provider = CreateServices([]);
        var cookieOptions = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/secure";
        var context = CreateCookieRedirectContext(
            httpContext,
            cookieOptions,
            "https://api.example.com/api/auth/login");

        await cookieOptions.Events.OnRedirectToLogin(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task AddOidcStarterBff_returns_forbidden_for_api_access_denied_redirect()
    {
        using var provider = CreateServices([]);
        var cookieOptions = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/secure";
        var context = CreateCookieRedirectContext(
            httpContext,
            cookieOptions,
            "https://api.example.com/api/auth/denied");

        await cookieOptions.Events.OnRedirectToAccessDenied(context);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    private static ServiceProvider CreateServices(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration(values));

        return services.BuildServiceProvider();
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateCookieRedirectContext(
        HttpContext httpContext,
        CookieAuthenticationOptions cookieOptions,
        string redirectUri)
        => new(
            httpContext,
            new AuthenticationScheme(
                CookieAuthenticationDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme,
                typeof(CookieAuthenticationHandler)),
            cookieOptions,
            new AuthenticationProperties(),
            redirectUri);

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class CustomRoleMapper : IOidcStarterRoleMapper
    {
        public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
        {
            yield return "custom";
        }
    }
}
