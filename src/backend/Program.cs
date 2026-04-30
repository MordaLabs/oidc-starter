using Backend.Configuration;
using Backend.Security;
using Backend.Services.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<StarterOptions>(
    builder.Configuration.GetSection(StarterOptions.SectionName));
builder.Services.Configure<OidcOptions>(
    builder.Configuration.GetSection(OidcOptions.SectionName));

var frontendOrigin = builder.Configuration
    .GetSection(StarterOptions.SectionName)
    .GetValue<string>(nameof(StarterOptions.FrontendOrigin));
var starterSettings = builder.Configuration
    .GetSection(StarterOptions.SectionName)
    .Get<StarterOptions>() ?? new StarterOptions();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.ForwardLimit = 1;

    foreach (var host in starterSettings.AllowedForwardedHosts.Where(static host => !string.IsNullOrWhiteSpace(host)))
    {
        options.AllowedHosts.Add(host);
    }
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            policy.WithOrigins(frontendOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var oidcSettings = builder.Configuration
    .GetSection(OidcOptions.SectionName)
    .Get<OidcOptions>() ?? new OidcOptions();

builder.Services.AddAuthentication(options =>
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

builder.Services.AddAuthorization();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<CsrfOriginValidator>();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
