using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.IdentityModel.Tokens;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Controllers;
using OidcStarter.AspNetCore.Bff.Security;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Extensions;

public static class OidcStarterBffServiceCollectionExtensions
{
    /// <summary>
    /// Adds opt-in Google authentication using the supplied Google handler configuration section.
    /// </summary>
    /// <remarks>
    /// The Google handler uses the common BFF cookie session and local-session-only logout behavior.
    /// </remarks>
    public static IServiceCollection AddOidcStarterGoogle(
        this IServiceCollection services,
        IConfigurationSection configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);
        ValidateGoogleCallbackPath(configurationSection["CallbackPath"]);

        services.AddAuthentication()
            .AddGoogle(OidcStarterGoogleDefaults.AuthenticationScheme, options =>
            {
                configurationSection.Bind(options);
                options.ClaimActions.Add(new CustomJsonClaimAction(
                    ExternalIdentityClaimTypes.EmailVerified,
                    ClaimValueTypes.Boolean,
                    static userInfo => userInfo.TryGetProperty("verified_email", out var verifiedEmail)
                        && (verifiedEmail.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        ? verifiedEmail.GetBoolean().ToString()
                        : null));
                options.ClaimActions.MapJsonKey(
                    ExternalIdentityClaimTypes.PictureUrl,
                    "picture");
            });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<GoogleOptions>, OidcStarterGoogleOptionsPostConfigure>());
        services.AddOptions<GoogleOptions>(OidcStarterGoogleDefaults.AuthenticationScheme)
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.ClientId),
                "Google ClientId is required.")
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.ClientSecret),
                "Google ClientSecret is required.")
            .Validate(
                static options => IsValidGoogleCallbackPath(options.CallbackPath.Value),
                "Google CallbackPath must be a local absolute path.")
            .ValidateOnStart();
        services.AddOidcStarterLoginProvider(
            OidcStarterGoogleDefaults.ProviderId,
            OidcStarterGoogleDefaults.DisplayName,
            OidcStarterGoogleDefaults.AuthenticationScheme);

        return services;
    }

    /// <summary>
    /// Registers metadata for an existing authentication scheme as an opt-in login provider.
    /// </summary>
    /// <remarks>
    /// Register the referenced authentication scheme separately before the provider is challenged.
    /// This method does not add or configure an authentication handler.
    /// Generic provider registrations use local-session-only logout behavior.
    /// </remarks>
    public static IServiceCollection AddOidcStarterLoginProvider(
        this IServiceCollection services,
        string providerId,
        string displayName,
        string authenticationScheme)
    {
        return AddLoginProvider(
            services,
            providerId,
            displayName,
            authenticationScheme,
            supportsRemoteSignOut: false);
    }

    public static IServiceCollection AddOidcStarterRoleMapper<TMapper>(this IServiceCollection services)
        where TMapper : class, IOidcStarterRoleMapper
    {
        services.AddSingleton<IOidcStarterRoleMapper, TMapper>();

        return services;
    }

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
        AddLoginProvider(
            services,
            "oidc",
            "OpenID Connect",
            OpenIdConnectDefaults.AuthenticationScheme,
            supportsRemoteSignOut: true);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OidcStarterBffOptions>, LoginProviderRegistryOptionsPostConfigure>());
        services.AddOptions<OidcStarterBffOptions>().ValidateOnStart();

        var bffSettings = configuration
            .GetSection(OidcStarterBffOptions.SectionName)
            .Get<OidcStarterBffOptions>() ?? new OidcStarterBffOptions();

        ConfigureForwardedHeaders(services, bffSettings);
        ConfigureCors(services, bffSettings);
        ConfigureAuthentication(services, configuration, bffSettings);
        ConfigureAntiforgery(services, bffSettings);
        ConfigureAuthorization(services, bffSettings);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOidcStarterRoleMapper, DefaultOidcStarterRoleMapper>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IClaimsTransformation, OidcStarterRoleClaimsTransformation>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<CsrfOriginValidator>();

        return services;
    }

    internal static IServiceCollection AddLoginProvider(
        IServiceCollection services,
        string providerId,
        string displayName,
        string authenticationScheme,
        bool supportsRemoteSignOut)
    {
        ArgumentNullException.ThrowIfNull(services);
        LoginProviderRegistration.Validate(providerId, displayName, authenticationScheme);

        services.AddSingleton(new LoginProviderDescriptor(
            providerId,
            displayName,
            authenticationScheme,
            supportsRemoteSignOut));

        return services;
    }

    private static void ValidateGoogleCallbackPath(string? callbackPath)
    {
        if (callbackPath is not null && !IsValidGoogleCallbackPath(callbackPath))
        {
            throw new ArgumentException(
                "Google CallbackPath must be a local absolute path.",
                nameof(callbackPath));
        }
    }

    private static bool IsValidGoogleCallbackPath(string? callbackPath)
        => !string.IsNullOrWhiteSpace(callbackPath)
            && callbackPath[0] == '/'
            && !callbackPath.StartsWith("//", StringComparison.Ordinal)
            && !callbackPath.Contains('?')
            && !callbackPath.Contains('#');

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

            foreach (var proxy in bffSettings.KnownForwardedProxies.Where(static proxy => !string.IsNullOrWhiteSpace(proxy)))
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var network in bffSettings.KnownForwardedNetworks.Where(static network => !string.IsNullOrWhiteSpace(network)))
            {
                if (TryParseNetwork(network, out var parsedNetwork))
                {
                    options.KnownNetworks.Add(parsedNetwork);
                }
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

    private static void ConfigureAuthentication(
        IServiceCollection services,
        IConfiguration configuration,
        OidcStarterBffOptions bffSettings)
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
                options.Cookie.Name = bffSettings.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.Path = "/";
                options.Cookie.SameSite = bffSettings.CookieSameSite;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = bffSettings.SessionLifetime;
                options.SlidingExpiration = bffSettings.SlidingExpiration;
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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = bffSettings.NameClaimType,
                    RoleClaimType = bffSettings.RoleClaimType
                };
                options.CorrelationCookie.SameSite = bffSettings.CookieSameSite;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SameSite = bffSettings.CookieSameSite;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ClaimActions.MapJsonKey(bffSettings.NameClaimType, bffSettings.NameClaimType);
                options.ClaimActions.MapJsonKey(bffSettings.RoleClaimType, bffSettings.RoleClaimType);
                options.Events.OnTicketReceived = context =>
                {
                    var principal = context.Principal;
                    var accessToken = context.Properties?.GetTokenValue("access_token");

                    if (principal is null || string.IsNullOrWhiteSpace(bffSettings.RoleClaimType))
                    {
                        return Task.CompletedTask;
                    }

                    AddMappedRoleClaims(
                        principal,
                        new OidcStarterRoleMappingContext(principal, accessToken),
                        bffSettings,
                        context.HttpContext.RequestServices.GetServices<IOidcStarterRoleMapper>());

                    return Task.CompletedTask;
                };

                options.Scope.Clear();
                foreach (var scope in oidcSettings.Scopes.Where(static scope => !string.IsNullOrWhiteSpace(scope)))
                {
                    options.Scope.Add(scope);
                }
            });
    }

    private static void ConfigureAuthorization(IServiceCollection services, OidcStarterBffOptions bffSettings)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                OidcStarterBffPolicies.AuthenticatedUser,
                policy => RequireAuthenticatedBffSession(policy));

            if (bffSettings.RequiredScopes.Any(static scope => !string.IsNullOrWhiteSpace(scope)))
            {
                options.AddPolicy(
                    OidcStarterBffPolicies.ConfiguredRequiredScopes,
                    policy => RequireAuthenticatedBffSession(policy)
                        .RequireOidcStarterScopes(bffSettings.RequiredScopes));
            }

            if (bffSettings.RequiredClaims.Any(static claim => !string.IsNullOrWhiteSpace(claim.Type)))
            {
                options.AddPolicy(
                    OidcStarterBffPolicies.ConfiguredRequiredClaims,
                    policy => RequireAuthenticatedBffSession(policy)
                        .RequireOidcStarterClaims(bffSettings.RequiredClaims));
            }
        });
    }

    private static Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder RequireAuthenticatedBffSession(
        Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder policy)
    {
        policy.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
        return policy.RequireAuthenticatedUser();
    }

    private static void AddMappedRoleClaims(
        ClaimsPrincipal principal,
        OidcStarterRoleMappingContext mappingContext,
        OidcStarterBffOptions bffSettings,
        IEnumerable<IOidcStarterRoleMapper> roleMappers)
    {
        var existingRoles = principal
            .FindAll(bffSettings.RoleClaimType)
            .Select(static claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mappedRoles = roleMappers
            .SelectMany(mapper => mapper.GetRoles(mappingContext))
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(role => !existingRoles.Contains(role))
            .ToArray();

        if (mappedRoles.Length == 0)
        {
            return;
        }

        principal.AddIdentity(new ClaimsIdentity(
            mappedRoles.Select(role => new Claim(bffSettings.RoleClaimType, role)),
            authenticationType: "OidcStarterRoleMapping",
            nameType: bffSettings.NameClaimType,
            roleType: bffSettings.RoleClaimType));
    }

    private static void ConfigureAntiforgery(IServiceCollection services, OidcStarterBffOptions bffSettings)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = bffSettings.AntiforgeryHeaderName;
            options.Cookie.Name = bffSettings.AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = bffSettings.CookieSameSite;
            options.Cookie.SecurePolicy = bffSettings.AntiforgeryCookieSecurePolicy;
            options.Cookie.IsEssential = true;
        });
    }

    private static bool TryParseNetwork(string value, out Microsoft.AspNetCore.HttpOverrides.IPNetwork network)
    {
        network = null!;
        var parts = value.Split('/', StringSplitOptions.TrimEntries);

        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        network = new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength);
        return true;
    }
}
