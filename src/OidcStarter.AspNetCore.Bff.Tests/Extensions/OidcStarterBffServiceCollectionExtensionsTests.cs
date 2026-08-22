using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Extensions;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Tests.Extensions;

public sealed class OidcStarterBffServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOidcStarterFacebook_returns_the_original_collection_and_rejects_null_arguments()
    {
        var services = new ServiceCollection();
        var configurationSection = CreateFacebookConfigurationSection();

        var result = services.AddOidcStarterFacebook(configurationSection);

        Assert.Same(services, result);
        Assert.Throws<ArgumentNullException>(() =>
            OidcStarterBffServiceCollectionExtensions.AddOidcStarterFacebook(null!, configurationSection));
        Assert.Throws<ArgumentNullException>(() => services.AddOidcStarterFacebook(null!));
    }

    [Fact]
    public void AddOidcStarterBff_does_not_register_facebook_by_default()
    {
        using var provider = CreateServices(CreateValidOidcConfiguration());
        var registry = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value.LoginProviders;

        Assert.DoesNotContain(registry.Providers, provider => provider.Id == OidcStarterFacebookDefaults.ProviderId);
    }

    [Fact]
    public async Task AddOidcStarterFacebook_registers_the_official_handler_with_bff_session_options()
    {
        using var provider = CreateFacebookServices(
            new Dictionary<string, string?>
            {
                ["Facebook:AppId"] = "facebook-app-id",
                ["Facebook:AppSecret"] = "facebook-app-secret"
            },
            new Dictionary<string, string?>
            {
                ["Starter:CookieSameSite"] = "Strict"
            });
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var facebookScheme = await schemeProvider.GetSchemeAsync(OidcStarterFacebookDefaults.AuthenticationScheme);
        var facebookOptions = provider
            .GetRequiredService<IOptionsMonitor<FacebookOptions>>()
            .Get(OidcStarterFacebookDefaults.AuthenticationScheme);
        var registry = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value.LoginProviders;

        Assert.NotNull(facebookScheme);
        Assert.Equal(typeof(FacebookHandler), facebookScheme.HandlerType);
        Assert.Equal("facebook-app-id", facebookOptions.AppId);
        Assert.Equal("facebook-app-secret", facebookOptions.AppSecret);
        Assert.Equal("https://www.facebook.com/v26.0/dialog/oauth", facebookOptions.AuthorizationEndpoint);
        Assert.Equal("https://graph.facebook.com/v26.0/oauth/access_token", facebookOptions.TokenEndpoint);
        Assert.Equal("https://graph.facebook.com/v26.0/me", facebookOptions.UserInformationEndpoint);
        Assert.DoesNotContain("/v14.0/", facebookOptions.AuthorizationEndpoint);
        Assert.DoesNotContain("/v14.0/", facebookOptions.TokenEndpoint);
        Assert.DoesNotContain("/v14.0/", facebookOptions.UserInformationEndpoint);
        Assert.All(
            new[]
            {
                facebookOptions.AuthorizationEndpoint,
                facebookOptions.TokenEndpoint,
                facebookOptions.UserInformationEndpoint
            },
            endpoint => Assert.Contains($"/{OidcStarterFacebookDefaults.GraphApiVersion}/", endpoint));
        Assert.Equal(new PathString("/signin-facebook"), facebookOptions.CallbackPath);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, facebookOptions.SignInScheme);
        Assert.Equal(SameSiteMode.Strict, facebookOptions.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, facebookOptions.CorrelationCookie.SecurePolicy);
        Assert.True(facebookOptions.UsePkce);
        Assert.True(facebookOptions.SendAppSecretProof);
        Assert.False(facebookOptions.SaveTokens);
        Assert.Equal(["email"], facebookOptions.Scope);
        Assert.Contains("name", facebookOptions.Fields);
        Assert.Contains("first_name", facebookOptions.Fields);
        Assert.Contains("last_name", facebookOptions.Fields);
        Assert.Contains("email", facebookOptions.Fields);
        Assert.Equal(1, facebookOptions.Fields.Count(field => field == "picture"));
        var expectedFields = new[] { "name", "first_name", "last_name", "email", "picture" };
        Assert.All(
            facebookOptions.Fields,
            field => Assert.Contains(field, expectedFields));
        var facebook = Assert.Single(registry.Providers.Where(provider => provider.Id == "facebook"));
        Assert.Equal("Facebook", facebook.DisplayName);
        Assert.Equal(OidcStarterFacebookDefaults.AuthenticationScheme, facebook.AuthenticationScheme);
        Assert.False(facebook.SupportsRemoteSignOut);
        Assert.Equal("oidc", registry.DefaultProvider.Id);
    }

    [Fact]
    public void AddOidcStarterFacebook_preserves_all_configured_graph_api_endpoint_overrides()
    {
        using var provider = CreateFacebookServices(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret",
            ["Facebook:AuthorizationEndpoint"] = "https://login.example.test/custom-authorize",
            ["Facebook:TokenEndpoint"] = "https://tokens.example.test/custom-token",
            ["Facebook:UserInformationEndpoint"] = "https://profile.example.test/custom-user-info"
        });

        var facebookOptions = GetFacebookOptions(provider);

        Assert.Equal("https://login.example.test/custom-authorize", facebookOptions.AuthorizationEndpoint);
        Assert.Equal("https://tokens.example.test/custom-token", facebookOptions.TokenEndpoint);
        Assert.Equal("https://profile.example.test/custom-user-info", facebookOptions.UserInformationEndpoint);
    }

    [Fact]
    public void AddOidcStarterFacebook_preserves_partial_graph_api_endpoint_overrides()
    {
        using var provider = CreateFacebookServices(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret",
            ["Facebook:TokenEndpoint"] = "https://tokens.example.test/custom-token"
        });

        var facebookOptions = GetFacebookOptions(provider);

        Assert.Equal(OidcStarterFacebookDefaults.AuthorizationEndpoint, facebookOptions.AuthorizationEndpoint);
        Assert.Equal("https://tokens.example.test/custom-token", facebookOptions.TokenEndpoint);
        Assert.Equal(OidcStarterFacebookDefaults.UserInformationEndpoint, facebookOptions.UserInformationEndpoint);
    }

    [Theory]
    [InlineData("AuthorizationEndpoint", "")]
    [InlineData("TokenEndpoint", "not-a-uri")]
    [InlineData("UserInformationEndpoint", " ")]
    public void AddOidcStarterFacebook_fails_closed_for_blank_or_invalid_configured_endpoints(
        string endpointName,
        string endpointValue)
    {
        using var provider = CreateFacebookServices(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret",
            [$"Facebook:{endpointName}"] = endpointValue
        });

        var exception = Record.Exception(() => GetFacebookOptions(provider));

        Assert.True(exception is ArgumentException or OptionsValidationException);
    }

    [Theory]
    [InlineData("Facebook:AppId")]
    [InlineData("Facebook:AppSecret")]
    public void AddOidcStarterFacebook_fails_closed_when_essential_configuration_is_missing_or_blank(string key)
    {
        var missingConfiguration = new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret"
        };
        missingConfiguration.Remove(key);
        using var missingProvider = CreateFacebookServices(missingConfiguration);

        Assert.ThrowsAny<ArgumentException>(() => GetFacebookOptions(missingProvider));

        using var blankProvider = CreateFacebookServices(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret",
            [key] = " "
        });

        Assert.Throws<OptionsValidationException>(() => GetFacebookOptions(blankProvider));
    }

    [Fact]
    public void AddOidcStarterFacebook_maps_standard_and_normalized_picture_claims()
    {
        using var provider = CreateFacebookServices(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret"
        });
        using var userInfo = JsonDocument.Parse("""
            {
              "id": "facebook-user-123",
              "name": "Facebook User",
              "first_name": "Facebook",
              "last_name": "User",
              "email": "user@example.test",
              "picture": { "data": { "url": "https://example.test/picture.jpg" } }
            }
            """);
        var identity = new ClaimsIdentity();

        RunClaimActions(GetFacebookOptions(provider), userInfo.RootElement, identity);

        Assert.Equal("facebook-user-123", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Facebook User", identity.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Facebook", identity.FindFirst(ClaimTypes.GivenName)?.Value);
        Assert.Equal("User", identity.FindFirst(ClaimTypes.Surname)?.Value);
        Assert.Equal("user@example.test", identity.FindFirst(ClaimTypes.Email)?.Value);
        var pictureUrlClaim = Assert.Single(identity.FindAll(ExternalIdentityClaimTypes.PictureUrl));
        Assert.Equal("https://example.test/picture.jpg", pictureUrlClaim.Value);
        Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.EmailVerified));
        Assert.Null(identity.FindFirst("picture"));
        Assert.Null(identity.FindFirst("access_token"));
    }

    [Fact]
    public void AddOidcStarterFacebook_tolerates_missing_email_and_incomplete_picture_data()
    {
        using var provider = CreateFacebookServices(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "facebook-app-id",
            ["Facebook:AppSecret"] = "facebook-app-secret"
        });
        var incompletePicturePayloads = new[]
        {
            """{ "id": "facebook-user-123" }""",
            """{ "id": "facebook-user-123", "picture": {} }""",
            """{ "id": "facebook-user-123", "picture": { "data": {} } }""",
            """{ "id": "facebook-user-123", "picture": { "data": { "url": null } } }""",
            """{ "id": "facebook-user-123", "picture": { "data": { "url": "" } } }""",
            """{ "id": "facebook-user-123", "picture": { "data": { "url": "   " } } }""",
            """{ "id": "facebook-user-123", "picture": { "data": { "url": {} } } }"""
        };

        foreach (var userInfoJson in incompletePicturePayloads)
        {
            using var userInfo = JsonDocument.Parse(userInfoJson);
            var identity = new ClaimsIdentity();

            RunClaimActions(GetFacebookOptions(provider), userInfo.RootElement, identity);

            Assert.Equal("facebook-user-123", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            Assert.Null(identity.FindFirst(ClaimTypes.Email));
            Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.PictureUrl));
            Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.EmailVerified));
        }
    }

    [Fact]
    public void AddOidcStarterGitHub_returns_the_original_collection_and_rejects_null_arguments()
    {
        var services = new ServiceCollection();
        var configurationSection = CreateGitHubConfigurationSection();

        var result = services.AddOidcStarterGitHub(configurationSection);

        Assert.Same(services, result);
        Assert.Throws<ArgumentNullException>(() =>
            OidcStarterBffServiceCollectionExtensions.AddOidcStarterGitHub(null!, configurationSection));
        Assert.Throws<ArgumentNullException>(() => services.AddOidcStarterGitHub(null!));
    }

    [Fact]
    public void AddOidcStarterBff_does_not_register_github_by_default()
    {
        using var provider = CreateServices(CreateValidOidcConfiguration());
        var registry = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value.LoginProviders;

        Assert.DoesNotContain(registry.Providers, provider => provider.Id == OidcStarterGitHubDefaults.ProviderId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddOidcStarterGitHub_registers_provider_metadata_before_or_after_bff(bool githubFirst)
    {
        var services = new ServiceCollection();
        var githubConfiguration = CreateGitHubConfigurationSection();

        if (githubFirst)
        {
            services.AddOidcStarterGitHub(githubConfiguration);
            services.AddOidcStarterBff(CreateConfiguration([]));
        }
        else
        {
            services.AddOidcStarterBff(CreateConfiguration([]));
            services.AddOidcStarterGitHub(githubConfiguration);
        }

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value.LoginProviders;

        var github = Assert.Single(registry.Providers.Where(provider => provider.Id == "github"));
        Assert.Equal("GitHub", github.DisplayName);
        Assert.Equal(OidcStarterGitHubDefaults.AuthenticationScheme, github.AuthenticationScheme);
        Assert.False(github.SupportsRemoteSignOut);
        Assert.Equal("oidc", registry.DefaultProvider.Id);
    }

    [Fact]
    public async Task AddOidcStarterGitHub_registers_the_official_handler_with_bff_session_options()
    {
        using var provider = CreateGitHubServices(
            new Dictionary<string, string?>
            {
                ["GitHub:ClientId"] = "github-client-id",
                ["GitHub:ClientSecret"] = "github-client-secret"
            },
            new Dictionary<string, string?>
            {
                ["Starter:CookieSameSite"] = "Strict"
            });
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var githubScheme = await schemeProvider.GetSchemeAsync(OidcStarterGitHubDefaults.AuthenticationScheme);
        var githubOptions = GetGitHubOptions(provider);
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.NotNull(githubScheme);
        Assert.Equal("github-client-id", githubOptions.ClientId);
        Assert.Equal("github-client-secret", githubOptions.ClientSecret);
        Assert.Equal("https://github.com/login/oauth/authorize", githubOptions.AuthorizationEndpoint);
        Assert.Equal("https://github.com/login/oauth/access_token", githubOptions.TokenEndpoint);
        Assert.Equal("https://api.github.com/user", githubOptions.UserInformationEndpoint);
        Assert.Equal("https://api.github.com/user/emails", githubOptions.UserEmailsEndpoint);
        Assert.Equal(new PathString("/signin-github"), githubOptions.CallbackPath);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, githubOptions.SignInScheme);
        Assert.Equal(SameSiteMode.Strict, githubOptions.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, githubOptions.CorrelationCookie.SecurePolicy);
        Assert.True(githubOptions.UsePkce);
        Assert.False(githubOptions.SaveTokens);
        Assert.Equal(["user:email"], githubOptions.Scope);
        Assert.Equal(1, githubOptions.Scope.Count(scope => scope == "user:email"));
        Assert.DoesNotContain(githubOptions.Scope, scope =>
            scope is "user" or "read:user" or "repo" or "read:org" or "gist" or "notifications");
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authenticationOptions.DefaultScheme);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, authenticationOptions.DefaultChallengeScheme);
    }

    [Fact]
    public void AddOidcStarterGitHub_binds_a_configured_local_callback_path()
    {
        using var provider = CreateGitHubServices(new Dictionary<string, string?>
        {
            ["GitHub:ClientId"] = "github-client-id",
            ["GitHub:ClientSecret"] = "github-client-secret",
            ["GitHub:CallbackPath"] = "/custom-signin-github"
        });

        Assert.Equal(new PathString("/custom-signin-github"), GetGitHubOptions(provider).CallbackPath);
    }

    [Theory]
    [InlineData("GitHub:ClientId")]
    [InlineData("GitHub:ClientSecret")]
    public void AddOidcStarterGitHub_fails_closed_when_essential_configuration_is_missing_or_blank(string key)
    {
        var missingConfiguration = new Dictionary<string, string?>
        {
            ["GitHub:ClientId"] = "github-client-id",
            ["GitHub:ClientSecret"] = "github-client-secret"
        };
        missingConfiguration.Remove(key);
        using var missingProvider = CreateGitHubServices(missingConfiguration);

        Assert.ThrowsAny<ArgumentException>(() => GetGitHubOptions(missingProvider));

        using var blankProvider = CreateGitHubServices(new Dictionary<string, string?>
        {
            ["GitHub:ClientId"] = "github-client-id",
            ["GitHub:ClientSecret"] = "github-client-secret",
            [key] = " "
        });

        Assert.Throws<OptionsValidationException>(() => GetGitHubOptions(blankProvider));
    }

    [Theory]
    [InlineData("")]
    [InlineData("signin-github")]
    [InlineData("//signin-github")]
    [InlineData("/signin-github?next=/")]
    [InlineData("/signin-github#fragment")]
    public void AddOidcStarterGitHub_rejects_blank_or_invalid_callback_paths(string callbackPath)
    {
        var services = new ServiceCollection();
        var configurationSection = CreateGitHubConfigurationSection(new Dictionary<string, string?>
        {
            ["GitHub:CallbackPath"] = callbackPath
        });

        Assert.Throws<ArgumentException>(() => services.AddOidcStarterGitHub(configurationSection));
    }

    [Fact]
    public void AddOidcStarterGitHub_maps_standard_and_normalized_identity_claims()
    {
        using var provider = CreateGitHubServices(new Dictionary<string, string?>
        {
            ["GitHub:ClientId"] = "github-client-id",
            ["GitHub:ClientSecret"] = "github-client-secret"
        });
        using var userInfo = JsonDocument.Parse("""
            {
              "id": 123,
              "login": "octocat",
              "name": "The Octocat",
              "email": "octocat@example.test",
              "avatar_url": "https://example.test/avatar.png"
            }
            """);
        var identity = new ClaimsIdentity();

        RunClaimActions(GetGitHubOptions(provider), userInfo.RootElement, identity);

        Assert.Equal("123", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("octocat", identity.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("octocat", identity.FindFirst("preferred_username")?.Value);
        Assert.Equal("The Octocat", identity.FindFirst("name")?.Value);
        Assert.Equal("octocat@example.test", identity.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal(
            "https://example.test/avatar.png",
            identity.FindFirst(ExternalIdentityClaimTypes.PictureUrl)?.Value);
        Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.EmailVerified));
        Assert.Null(identity.FindFirst("access_token"));
        Assert.Null(identity.FindFirst("raw_user_info"));
    }

    [Theory]
    [InlineData("{ \"id\": 123, \"login\": \"octocat\" }")]
    [InlineData("{ \"id\": 123, \"login\": \"octocat\", \"name\": \"   \", \"avatar_url\": null }")]
    [InlineData("{ \"id\": 123, \"login\": \"octocat\", \"avatar_url\": \"\" }")]
    [InlineData("{ \"id\": 123, \"login\": \"octocat\", \"avatar_url\": \"   \" }")]
    [InlineData("{ \"id\": 123, \"login\": \"octocat\", \"avatar_url\": {} }")]
    public void AddOidcStarterGitHub_tolerates_missing_profile_values_and_unusable_avatars(string userInfoJson)
    {
        using var provider = CreateGitHubServices(new Dictionary<string, string?>
        {
            ["GitHub:ClientId"] = "github-client-id",
            ["GitHub:ClientSecret"] = "github-client-secret"
        });
        using var userInfo = JsonDocument.Parse(userInfoJson);
        var identity = new ClaimsIdentity();

        RunClaimActions(GetGitHubOptions(provider), userInfo.RootElement, identity);

        Assert.Equal("123", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("octocat", identity.FindFirst("preferred_username")?.Value);
        Assert.Null(identity.FindFirst("name"));
        Assert.Null(identity.FindFirst(ClaimTypes.Email));
        Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.PictureUrl));
        Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.EmailVerified));
    }

    [Fact]
    public void AddOidcStarterGoogle_returns_the_original_collection_and_rejects_null_arguments()
    {
        var services = new ServiceCollection();
        var configurationSection = CreateGoogleConfigurationSection();

        var result = services.AddOidcStarterGoogle(configurationSection);

        Assert.Same(services, result);
        Assert.Throws<ArgumentNullException>(() =>
            OidcStarterBffServiceCollectionExtensions.AddOidcStarterGoogle(null!, configurationSection));
        Assert.Throws<ArgumentNullException>(() => services.AddOidcStarterGoogle(null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddOidcStarterGoogle_registers_provider_metadata_before_or_after_bff(bool googleFirst)
    {
        var services = new ServiceCollection();
        var googleConfiguration = CreateGoogleConfigurationSection();

        if (googleFirst)
        {
            services.AddOidcStarterGoogle(googleConfiguration);
            services.AddOidcStarterBff(CreateConfiguration([]));
        }
        else
        {
            services.AddOidcStarterBff(CreateConfiguration([]));
            services.AddOidcStarterGoogle(googleConfiguration);
        }

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value.LoginProviders;

        var google = Assert.Single(registry.Providers.Where(provider => provider.Id == "google"));
        Assert.Equal("Google", google.DisplayName);
        Assert.Equal(OidcStarterGoogleDefaults.AuthenticationScheme, google.AuthenticationScheme);
        Assert.False(google.SupportsRemoteSignOut);
        Assert.Equal("oidc", registry.DefaultProvider.Id);
    }

    [Fact]
    public async Task AddOidcStarterGoogle_registers_the_official_handler_with_bff_session_options()
    {
        using var provider = CreateGoogleServices(
            new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "google-client-id",
                ["Google:ClientSecret"] = "google-client-secret"
            },
            new Dictionary<string, string?>
            {
                ["Starter:CookieSameSite"] = "Strict"
            });
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var googleScheme = await schemeProvider.GetSchemeAsync(OidcStarterGoogleDefaults.AuthenticationScheme);
        var oidcScheme = await schemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme);
        var googleOptions = provider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(OidcStarterGoogleDefaults.AuthenticationScheme);
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        Assert.NotNull(googleScheme);
        Assert.Equal(typeof(GoogleHandler), googleScheme.HandlerType);
        Assert.NotNull(oidcScheme);
        Assert.NotEqual(oidcScheme.Name, googleScheme.Name);
        Assert.Equal("google-client-id", googleOptions.ClientId);
        Assert.Equal("google-client-secret", googleOptions.ClientSecret);
        Assert.Equal(new PathString("/signin-google"), googleOptions.CallbackPath);
        Assert.NotEqual(new PathString("/signin-oidc"), googleOptions.CallbackPath);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, googleOptions.SignInScheme);
        Assert.Equal(SameSiteMode.Strict, googleOptions.CorrelationCookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, googleOptions.CorrelationCookie.SecurePolicy);
        Assert.False(googleOptions.SaveTokens);
        Assert.Equal(["openid", "profile", "email"], googleOptions.Scope);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authenticationOptions.DefaultScheme);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, authenticationOptions.DefaultChallengeScheme);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddOidcStarterGoogle_maps_google_user_info_profile_claims(bool emailVerified)
    {
        using var provider = CreateGoogleServices(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "google-client-id",
            ["Google:ClientSecret"] = "google-client-secret"
        });
        var googleOptions = provider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(OidcStarterGoogleDefaults.AuthenticationScheme);
        using var userInfo = JsonDocument.Parse($$"""
            {
              "id": "google-user-123",
              "name": "Google User",
              "given_name": "Google",
              "family_name": "User",
              "link": "https://example.test/profile",
              "email": "user@example.test",
              "verified_email": {{emailVerified.ToString().ToLowerInvariant()}},
              "email_verified": false,
              "picture": "https://example.test/picture.jpg"
            }
            """);
        var identity = new ClaimsIdentity();

        RunClaimActions(googleOptions, userInfo.RootElement, identity);

        Assert.Equal("google-user-123", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Google User", identity.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Google", identity.FindFirst(ClaimTypes.GivenName)?.Value);
        Assert.Equal("User", identity.FindFirst(ClaimTypes.Surname)?.Value);
        Assert.Equal("https://example.test/profile", identity.FindFirst("urn:google:profile")?.Value);
        Assert.Equal("user@example.test", identity.FindFirst(ClaimTypes.Email)?.Value);
        var verifiedEmailClaim = Assert.Single(identity.FindAll(ExternalIdentityClaimTypes.EmailVerified));
        Assert.Equal(emailVerified.ToString(), verifiedEmailClaim.Value);
        Assert.Equal(ClaimValueTypes.Boolean, verifiedEmailClaim.ValueType);
        Assert.Equal(
            "https://example.test/picture.jpg",
            identity.FindFirst(ExternalIdentityClaimTypes.PictureUrl)?.Value);
    }

    [Fact]
    public void AddOidcStarterGoogle_ignores_unusable_verified_email_and_empty_picture_user_info_values()
    {
        using var provider = CreateGoogleServices(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "google-client-id",
            ["Google:ClientSecret"] = "google-client-secret"
        });
        var googleOptions = provider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(OidcStarterGoogleDefaults.AuthenticationScheme);
        using var userInfo = JsonDocument.Parse("""
            {
              "id": "google-user-123",
              "verified_email": "not-a-boolean",
              "picture": ""
            }
            """);
        var identity = new ClaimsIdentity();

        RunClaimActions(googleOptions, userInfo.RootElement, identity);

        Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.EmailVerified));
        Assert.Null(identity.FindFirst(ExternalIdentityClaimTypes.PictureUrl));
    }

    [Fact]
    public void AddOidcStarterGoogle_binds_a_configured_callback_path()
    {
        using var provider = CreateGoogleServices(new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "google-client-id",
            ["Google:ClientSecret"] = "google-client-secret",
            ["Google:CallbackPath"] = "/custom-signin-google"
        });

        var googleOptions = provider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(OidcStarterGoogleDefaults.AuthenticationScheme);

        Assert.Equal(new PathString("/custom-signin-google"), googleOptions.CallbackPath);
    }

    [Theory]
    [InlineData("Google:ClientId")]
    [InlineData("Google:ClientSecret")]
    public void AddOidcStarterGoogle_fails_closed_when_essential_configuration_is_missing(string missingKey)
    {
        var googleConfiguration = new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "google-client-id",
            ["Google:ClientSecret"] = "google-client-secret"
        };
        googleConfiguration.Remove(missingKey);

        using var provider = CreateGoogleServices(googleConfiguration);

        Assert.ThrowsAny<ArgumentException>(() => provider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(OidcStarterGoogleDefaults.AuthenticationScheme));
    }

    [Theory]
    [InlineData("Google:ClientId")]
    [InlineData("Google:ClientSecret")]
    public void AddOidcStarterGoogle_fails_closed_when_essential_configuration_is_blank(string blankKey)
    {
        var googleConfiguration = new Dictionary<string, string?>
        {
            ["Google:ClientId"] = "google-client-id",
            ["Google:ClientSecret"] = "google-client-secret",
            [blankKey] = " "
        };

        using var provider = CreateGoogleServices(googleConfiguration);

        Assert.Throws<OptionsValidationException>(() => provider
            .GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(OidcStarterGoogleDefaults.AuthenticationScheme));
    }

    [Theory]
    [InlineData("")]
    [InlineData("signin-google")]
    [InlineData("//signin-google")]
    public void AddOidcStarterGoogle_rejects_blank_or_invalid_callback_paths(string callbackPath)
    {
        var services = new ServiceCollection();
        var configurationSection = CreateGoogleConfigurationSection(new Dictionary<string, string?>
        {
            ["Google:CallbackPath"] = callbackPath
        });

        Assert.Throws<ArgumentException>(() => services.AddOidcStarterGoogle(configurationSection));
    }

    [Fact]
    public void AddOidcStarterGoogle_rejects_duplicate_registration_deterministically()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration([]));
        var configurationSection = CreateGoogleConfigurationSection();
        services.AddOidcStarterGoogle(configurationSection);
        services.AddOidcStarterGoogle(configurationSection);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider
            .GetRequiredService<IOptions<OidcStarterBffOptions>>().Value);
    }

    [Fact]
    public void AddOidcStarterGoogle_uses_a_configured_google_default_without_replacing_oidc_by_default()
    {
        using var provider = CreateGoogleServices(
            new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "google-client-id",
                ["Google:ClientSecret"] = "google-client-secret"
            },
            new Dictionary<string, string?>
            {
                ["Starter:DefaultLoginProvider"] = "google"
            });

        var registry = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value.LoginProviders;

        Assert.Equal("google", registry.DefaultProvider.Id);
    }

    [Fact]
    public async Task AddOidcStarterBff_registers_single_provider_authentication_scheme_defaults()
    {
        using var provider = CreateServices(CreateValidOidcConfiguration());
        var authenticationOptions = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        var cookieScheme = await schemeProvider.GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var oidcScheme = await schemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authenticationOptions.DefaultScheme);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, authenticationOptions.DefaultChallengeScheme);
        Assert.NotNull(cookieScheme);
        Assert.Equal(typeof(CookieAuthenticationHandler), cookieScheme.HandlerType);
        Assert.NotNull(oidcScheme);
        Assert.Equal(typeof(OpenIdConnectHandler), oidcScheme.HandlerType);
    }

    [Fact]
    public async Task AddOidcStarterLoginProvider_returns_the_original_collection_without_adding_a_handler()
    {
        var services = new ServiceCollection();
        services.AddAuthentication();

        var result = services.AddOidcStarterLoginProvider("google", "Google", "google-scheme");

        using var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Same(services, result);
        Assert.Null(await schemeProvider.GetSchemeAsync("google-scheme"));
    }

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    [InlineData("facebook")]
    [InlineData("entra-id")]
    [InlineData("oidc2")]
    public void AddOidcStarterLoginProvider_accepts_canonical_route_safe_provider_ids(string providerId)
    {
        var services = new ServiceCollection();

        services.AddOidcStarterLoginProvider(providerId, "Provider", "provider-scheme");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Google")]
    [InlineData(" google")]
    [InlineData("google/tenant")]
    [InlineData("google_test")]
    [InlineData("-google")]
    [InlineData("google-")]
    [InlineData("google--corp")]
    public void AddOidcStarterLoginProvider_rejects_noncanonical_or_unsafe_provider_ids(string providerId)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddOidcStarterLoginProvider(providerId, "Provider", "provider-scheme"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddOidcStarterLoginProvider_rejects_blank_display_names(string? displayName)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddOidcStarterLoginProvider("google", displayName!, "google-scheme"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddOidcStarterLoginProvider_rejects_blank_authentication_schemes(string? authenticationScheme)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddOidcStarterLoginProvider("google", "Google", authenticationScheme!));
    }

    [Fact]
    public void AddOidcStarterBff_preserves_the_existing_public_registration_signature()
    {
        var method = typeof(OidcStarterBffServiceCollectionExtensions).GetMethod(
            nameof(OidcStarterBffServiceCollectionExtensions.AddOidcStarterBff),
            [typeof(IServiceCollection), typeof(IConfiguration)]);

        Assert.NotNull(method);
        Assert.Equal(typeof(IServiceCollection), method.ReturnType);
    }

    [Fact]
    public void AddOidcStarterBff_registers_the_default_oidc_login_provider()
    {
        using var provider = CreateServices(CreateValidOidcConfiguration());
        var options = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value;
        var registry = options.LoginProviders;

        var registeredProvider = Assert.Single(registry.Providers);
        Assert.Equal("oidc", registeredProvider.Id);
        Assert.Equal("OpenID Connect", registeredProvider.DisplayName);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, registeredProvider.AuthenticationScheme);
        Assert.True(registeredProvider.SupportsRemoteSignOut);
        Assert.Same(registeredProvider, registry.DefaultProvider);
        Assert.Equal("oidc", options.DefaultLoginProvider);
        Assert.True(registry.TryGetProvider("OIDC", out var caseInsensitiveProvider));
        Assert.Same(registeredProvider, caseInsensitiveProvider);
        Assert.False(registry.TryGetProvider("unknown", out _));
    }

    [Fact]
    public void Login_provider_registry_returns_providers_in_deterministic_id_order()
    {
        var registry = new LoginProviderRegistry(
        [
            new LoginProviderDescriptor("zeta", "Zeta", "zeta-scheme"),
            new LoginProviderDescriptor("oidc", "OpenID Connect", OpenIdConnectDefaults.AuthenticationScheme),
            new LoginProviderDescriptor("alpha", "Alpha", "alpha-scheme")
        ],
        "oidc");

        Assert.Equal(["alpha", "oidc", "zeta"], registry.Providers.Select(static provider => provider.Id));
    }

    [Fact]
    public void AddOidcStarterBff_uses_a_configured_registered_default_login_provider()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration(new Dictionary<string, string?>
        {
            ["Starter:DefaultLoginProvider"] = "google"
        }));
        services.AddOidcStarterLoginProvider("google", "Google", "google-scheme");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value;

        Assert.Equal("google", options.DefaultLoginProvider);
        Assert.Equal("google", options.LoginProviders.DefaultProvider.Id);
        Assert.False(options.LoginProviders.Providers.Single(provider => provider.Id == "google").SupportsRemoteSignOut);
    }

    [Fact]
    public void Login_provider_registry_rejects_duplicate_provider_ids_case_insensitively()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new LoginProviderRegistry(
        [
            new LoginProviderDescriptor("google", "Google", "google-scheme"),
            new LoginProviderDescriptor("GOOGLE", "Google Workspace", "workspace-scheme")
        ],
        "google"));

        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void AddOidcStarterBff_rejects_duplicate_authentication_schemes()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration([]));
        services.AddOidcStarterLoginProvider("google", "Google", "shared-scheme");
        services.AddOidcStarterLoginProvider("github", "GitHub", "SHARED-SCHEME");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value);
    }

    [Fact]
    public void AddOidcStarterBff_rejects_collisions_with_the_built_in_oidc_provider()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration([]));
        services.AddOidcStarterLoginProvider("oidc", "Another OIDC", "another-scheme");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value);
    }

    [Fact]
    public void AddOidcStarterBff_rejects_collisions_with_the_built_in_oidc_scheme()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration([]));
        services.AddOidcStarterLoginProvider(
            "google",
            "Google",
            OpenIdConnectDefaults.AuthenticationScheme);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value);
    }

    [Fact]
    public void AddOidcStarterBff_fails_closed_when_the_configured_default_is_missing()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["Starter:DefaultLoginProvider"] = "google"
        });

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value);
    }

    [Fact]
    public void AddOidcStarterBff_fails_closed_when_the_configured_default_id_is_invalid()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["Starter:DefaultLoginProvider"] = "Google"
        });

        Assert.Throws<ArgumentException>(
            () => provider.GetRequiredService<IOptions<OidcStarterBffOptions>>().Value);
    }

    [Fact]
    public void AddOidcStarterBff_binds_supported_openid_connect_options()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["Oidc:Authority"] = "https://identity.example.test",
            ["Oidc:ClientId"] = "bff-client",
            ["Oidc:ClientSecret"] = "secret-value",
            ["Oidc:CallbackPath"] = "/custom-signin-oidc",
            ["Oidc:SignedOutCallbackPath"] = "/custom-signout-callback-oidc",
            ["Oidc:RequireHttpsMetadata"] = "false",
            ["Oidc:Scopes:0"] = "openid",
            ["Oidc:Scopes:1"] = "profile",
            ["Oidc:Scopes:2"] = "email"
        });

        var oidcOptions = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.Equal("https://identity.example.test", oidcOptions.Authority);
        Assert.Equal("bff-client", oidcOptions.ClientId);
        Assert.Equal("secret-value", oidcOptions.ClientSecret);
        Assert.Equal(new PathString("/custom-signin-oidc"), oidcOptions.CallbackPath);
        Assert.Equal(new PathString("/custom-signout-callback-oidc"), oidcOptions.SignedOutCallbackPath);
        Assert.False(oidcOptions.RequireHttpsMetadata);
        Assert.Equal(["openid", "profile", "email"], oidcOptions.Scope);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, oidcOptions.SignInScheme);
        Assert.True(oidcOptions.SaveTokens);
        Assert.True(oidcOptions.GetClaimsFromUserInfoEndpoint);
    }

    [Fact]
    public void AddOidcStarterBff_uses_default_openid_connect_callback_paths()
    {
        using var provider = CreateServices(CreateValidOidcConfiguration());

        var oidcOptions = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.Equal(new PathString("/signin-oidc"), oidcOptions.CallbackPath);
        Assert.Equal(new PathString("/signout-callback-oidc"), oidcOptions.SignedOutCallbackPath);
    }

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

    private static ServiceProvider CreateGoogleServices(
        Dictionary<string, string?> googleValues,
        Dictionary<string, string?>? bffValues = null)
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration(bffValues ?? new Dictionary<string, string?>()));
        services.AddOidcStarterGoogle(CreateGoogleConfigurationSection(googleValues));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateFacebookServices(
        Dictionary<string, string?> facebookValues,
        Dictionary<string, string?>? bffValues = null)
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration(bffValues ?? new Dictionary<string, string?>()));
        services.AddOidcStarterFacebook(CreateFacebookConfigurationSection(facebookValues));

        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateGitHubServices(
        Dictionary<string, string?> githubValues,
        Dictionary<string, string?>? bffValues = null)
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration(bffValues ?? new Dictionary<string, string?>()));
        services.AddOidcStarterGitHub(CreateGitHubConfigurationSection(githubValues));

        return services.BuildServiceProvider();
    }

    private static FacebookOptions GetFacebookOptions(ServiceProvider provider)
        => provider
            .GetRequiredService<IOptionsMonitor<FacebookOptions>>()
            .Get(OidcStarterFacebookDefaults.AuthenticationScheme);

    private static GitHubAuthenticationOptions GetGitHubOptions(ServiceProvider provider)
        => provider
            .GetRequiredService<IOptionsMonitor<GitHubAuthenticationOptions>>()
            .Get(OidcStarterGitHubDefaults.AuthenticationScheme);

    private static void RunClaimActions(
        OAuthOptions options,
        JsonElement userInfo,
        ClaimsIdentity identity)
    {
        foreach (var claimAction in options.ClaimActions)
        {
            claimAction.Run(userInfo, identity, "Google");
        }
    }

    private static IConfigurationSection CreateGoogleConfigurationSection(
        Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "google-client-id",
                ["Google:ClientSecret"] = "google-client-secret"
            })
            .Build()
            .GetSection("Google");

    private static IConfigurationSection CreateFacebookConfigurationSection(
        Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>
            {
                ["Facebook:AppId"] = "facebook-app-id",
                ["Facebook:AppSecret"] = "facebook-app-secret"
            })
            .Build()
            .GetSection("Facebook");

    private static IConfigurationSection CreateGitHubConfigurationSection(
        Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>
            {
                ["GitHub:ClientId"] = "github-client-id",
                ["GitHub:ClientSecret"] = "github-client-secret"
            })
            .Build()
            .GetSection("GitHub");

    private static Dictionary<string, string?> CreateValidOidcConfiguration()
        => new()
        {
            ["Oidc:Authority"] = "https://identity.example.test",
            ["Oidc:ClientId"] = "bff-client",
            ["Oidc:ClientSecret"] = "secret"
        };

    private sealed class CustomRoleMapper : IOidcStarterRoleMapper
    {
        public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
        {
            yield return "custom";
        }
    }
}
