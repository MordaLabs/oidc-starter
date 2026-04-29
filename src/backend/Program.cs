using Backend.Configuration;
using Backend.Services.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<StarterOptions>(
    builder.Configuration.GetSection(StarterOptions.SectionName));
builder.Services.Configure<OidcOptions>(
    builder.Configuration.GetSection(OidcOptions.SectionName));

var frontendOrigin = builder.Configuration
    .GetSection(StarterOptions.SectionName)
    .GetValue<string>(nameof(StarterOptions.FrontendOrigin));

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
        options.LoginPath = "/api/auth/login";
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

        options.Scope.Clear();
        foreach (var scope in oidcSettings.Scopes.Where(static scope => !string.IsNullOrWhiteSpace(scope)))
        {
            options.Scope.Add(scope);
        }
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
