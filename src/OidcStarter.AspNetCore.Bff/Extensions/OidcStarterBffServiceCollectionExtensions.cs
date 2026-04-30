using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Controllers;
using OidcStarter.AspNetCore.Bff.Security;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Extensions;

public static class OidcStarterBffServiceCollectionExtensions
{
    public static IServiceCollection AddOidcStarterBff(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);

        services.Configure<OidcStarterBffOptions>(
            configuration.GetSection(OidcStarterBffOptions.SectionName));
        services.Configure<OidcOptions>(
            configuration.GetSection(OidcOptions.SectionName));

        var bffSettings = configuration
            .GetSection(OidcStarterBffOptions.SectionName)
            .Get<OidcStarterBffOptions>() ?? new OidcStarterBffOptions();

        ConfigureForwardedHeaders(services, bffSettings);
        ConfigureCors(services, bffSettings);
        ConfigureAuthentication(services, configuration);

        services.AddAuthorization();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<CsrfOriginValidator>();

        return services;
    }

    private static void ConfigureForwardedHeaders(IServiceCollection services, OidcStarterBffOptions bffSettings)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto
                | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;

            foreach (var host in bffSettings.AllowedForwardedHosts.Where(static host => !string.IsNullOrWhiteSpace(host)))
            {
                options.AllowedHosts.Add(host);
            }
        });
    }

    private static void ConfigureCors(IServiceCollection services, OidcStarterBffOptions bffSettings)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (!string.IsNullOrWhiteSpace(bffSettings.FrontendOrigin))
                {
                    policy.WithOrigins(bffSettings.FrontendOrigin)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });
    }

    private static void ConfigureAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var oidcSettings = configuration
            .GetSection(OidcOptions.SectionName)
            .Get<OidcOptions>() ?? new OidcOptions();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-oidc-starter-bff";
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.LoginPath = "/api/auth/login";
                options.AccessDeniedPath = "/api/auth/denied";
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = oidcSettings.Authority;
                options.ClientId = oidcSettings.ClientId;
                options.ClientSecret = oidcSettings.ClientSecret;
                options.CallbackPath = oidcSettings.CallbackPath;
                options.SignedOutCallbackPath = oidcSettings.SignedOutCallbackPath;
                options.ResponseType = "code";
                options.RequireHttpsMetadata = oidcSettings.RequireHttpsMetadata;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.CorrelationCookie.SameSite = SameSiteMode.None;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = SameSiteMode.None;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Scope.Clear();
                foreach (var scope in oidcSettings.Scopes.Where(static scope => !string.IsNullOrWhiteSpace(scope)))
                {
                    options.Scope.Add(scope);
                }
            });
    }
}
