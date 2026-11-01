using System.Net;
using System.Security.Claims;
using Backend.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace Backend.Tests;

public sealed class InvitationSessionValidationTests
{
    private const string CookieName = "__Host-oidc-starter-bff";
    // Written by the package's login endpoint; tickets in these tests are protected
    // by the real Cookies handler, including this authentication property.
    private const string LoginProviderKey = "OidcStarter.AspNetCore.Bff.LoginProviderId";

    [Theory]
    [InlineData("google", "google-subject", false)]
    [InlineData("github", "123", false)]
    [InlineData("google", "google-subject", true)]
    [InlineData("github", "123", true)]
    public async Task Revoked_external_cookie_logout_stays_local(string provider, string subject, bool legacy)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        var invitation = await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject, mark: !legacy);
        var csrf = await fixture.GetCsrfAsync(cookie);
        Assert.True(await fixture.Store.RevokeInvitationAsync(invitation));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/test/protected", cookie)).StatusCode);

        using var response = await fixture.PostLogoutAsync(cookie, csrf);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, fixture.RemoteSignOutCalls);
        Assert.Equal(0, fixture.RemoteChallengeCalls);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Logout_exception_does_not_apply_to_get_or_other_post_paths(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        var invitation = await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject);
        Assert.True(await fixture.Store.RevokeInvitationAsync(invitation));

        using var getLogout = await fixture.GetAsync("/api/auth/logout", cookie);
        Assert.Contains(getLogout.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        using var otherPost = await fixture.PostAsync("/api/test/other", cookie);
        Assert.Contains(otherPost.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.PostAsync("/api/test/protected", cookie)).StatusCode);
        Assert.Equal(0, fixture.RemoteSignOutCalls);
        Assert.Equal(0, fixture.RemoteChallengeCalls);
    }

    [Theory]
    [InlineData("google", " ", false)]
    [InlineData("github", "not-numeric", false)]
    [InlineData("google", "google-subject", true)]
    public async Task Malformed_external_ticket_is_not_accepted_by_logout_exception(
        string provider, string subject, bool ambiguous)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        var validSubject = provider == "github" ? "123" : "google-subject";
        await fixture.BindAsync(provider, validSubject);
        var validCookie = await fixture.SignInAsync(provider, validSubject);
        var csrf = await fixture.GetCsrfAsync(validCookie);
        var malformed = await fixture.SignInAsync(provider, subject,
            extraSubject: ambiguous ? "other" : null);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", malformed)).StatusCode);
        using var logout = await fixture.PostLogoutAsync(malformed, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.Equal(0, fixture.RemoteSignOutCalls);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    public async Task Unknown_or_missing_provider_cannot_use_logout_exception(string provider)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        await fixture.BindAsync("google", "google-subject");
        var validCookie = await fixture.SignInAsync("google", "google-subject");
        var csrf = await fixture.GetCsrfAsync(validCookie);
        var malformed = await fixture.SignInAsync(provider, "google-subject", mark: false);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", malformed)).StatusCode);
        using var logout = await fixture.PostLogoutAsync(malformed, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        Assert.Equal(0, fixture.RemoteSignOutCalls);
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task External_logout_remains_local_when_sqlite_disappears(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject);
        var csrf = await fixture.GetCsrfAsync(cookie);
        File.Delete(fixture.Path);

        using var response = await fixture.PostLogoutAsync(cookie, csrf);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.RemoteSignOutCalls);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task Keycloak_logout_still_uses_oidc_remote_signout()
    {
        await using var fixture = await Fixture.StartAsync(true, true, initialize: false);
        var cookie = await fixture.SignInAsync("oidc", "keycloak-subject", mark: false);
        var csrf = await fixture.GetCsrfAsync(cookie);
        using var response = await fixture.PostLogoutAsync(cookie, csrf);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, fixture.RemoteSignOutCalls);
        Assert.Equal(0, fixture.RemoteChallengeCalls);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        Assert.False(File.Exists(fixture.Path));
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Both_flags_off_keep_external_logout_local(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(false, false, initialize: false);
        var cookie = await fixture.SignInAsync(provider, subject, mark: false);
        var csrf = await fixture.GetCsrfAsync(cookie);
        using var response = await fixture.PostLogoutAsync(cookie, csrf);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.RemoteSignOutCalls);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        Assert.False(File.Exists(fixture.Path));
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Bound_cookie_is_revalidated_and_revocation_deletes_it(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        var invitation = await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject);

        Assert.Equal(HttpStatusCode.OK, (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.True(await fixture.Store.RevokeInvitationAsync(invitation));

        using var denied = await fixture.GetAsync("/api/auth/me", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Contains(denied.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        Assert.DoesNotContain(subject, await denied.Content.ReadAsStringAsync());
        Assert.False(denied.Headers.Contains("Location"));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/test/protected", cookie)).StatusCode);
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Missing_binding_and_unavailable_database_deny_without_creating_it(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true, initialize: false);
        var cookie = await fixture.SignInAsync(provider, subject);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.False(File.Exists(fixture.Path));

        await fixture.Store.InitializeAsync();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Deleted_binding_or_database_invalidates_existing_cookie(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject);
        Assert.Equal(HttpStatusCode.OK, (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);

        await using (var connection = new SqliteConnection($"Data Source={fixture.Path};Mode=ReadWrite;Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM identity_bindings WHERE provider_id = $provider AND provider_subject = $subject";
            command.Parameters.AddWithValue("$provider", provider);
            command.Parameters.AddWithValue("$subject", subject);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);

        File.Delete(fixture.Path);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task Revalidation_never_redeems_an_approved_unbound_invitation()
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        var invitation = await fixture.Store.CreateInvitationAsync("google-google-subject@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        var cookie = await fixture.SignInAsync("google", "google-subject");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.Equal(InvitationDecisionReason.BindingMissing,
            (await fixture.Store.CheckBoundIdentityAsync("google", "google-subject")).Reason);
    }

    [Theory]
    [InlineData("google", "google-subject", "other")]
    [InlineData("github", "123", "456")]
    [InlineData("github", "not-numeric", "not-numeric")]
    public async Task Subject_mismatch_and_invalid_subject_deny(string provider, string subject, string marked)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        if (subject != "not-numeric") await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject, markedSubject: marked);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Ambiguous_subject_claims_deny(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject, extraSubject: "other");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Theory]
    [InlineData("google", "google-subject")]
    [InlineData("github", "123")]
    public async Task Legacy_cookie_uses_protected_provider_and_subject(string provider, string subject)
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        var invitation = await fixture.BindAsync(provider, subject);
        var cookie = await fixture.SignInAsync(provider, subject, mark: false);
        Assert.Equal(HttpStatusCode.OK, (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.True(await fixture.Store.RevokeInvitationAsync(invitation));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Fact]
    public async Task Unknown_or_missing_provider_and_cross_provider_binding_cannot_bypass()
    {
        await using var fixture = await Fixture.StartAsync(true, true);
        await fixture.BindAsync("github", "123");
        foreach (var provider in new[] { "google", "unknown", "" })
        {
            var cookie = await fixture.SignInAsync(provider, "123", mark: false);
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        }
    }

    [Fact]
    public async Task Marked_cookie_with_conflicting_provider_property_is_rejected()
    {
        await using var fixture = await Fixture.StartAsync(true, false);
        await fixture.BindAsync("google", "google-subject");
        var cookie = await fixture.SignInAsync("github", "google-subject",
            markedProvider: "google");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Enforcement_flags_are_independent(bool google, bool github)
    {
        await using var fixture = await Fixture.StartAsync(google, github, initialize: false);
        var googleCookie = await fixture.SignInAsync("google", "google-subject", mark: false);
        var githubCookie = await fixture.SignInAsync("github", "123", mark: false);
        Assert.Equal(google ? HttpStatusCode.Unauthorized : HttpStatusCode.OK,
            (await fixture.GetAsync("/api/auth/me", googleCookie)).StatusCode);
        Assert.Equal(github ? HttpStatusCode.Unauthorized : HttpStatusCode.OK,
            (await fixture.GetAsync("/api/auth/me", githubCookie)).StatusCode);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task Keycloak_and_anonymous_requests_never_depend_on_invitation_database()
    {
        await using var fixture = await Fixture.StartAsync(true, true, initialize: false);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me")).StatusCode);
        var cookie = await fixture.SignInAsync("oidc", "keycloak-subject", mark: false);
        Assert.Equal(HttpStatusCode.OK,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task Prior_event_runs_and_cannot_accept_revoked_cookie()
    {
        var calls = 0;
        await using var fixture = await Fixture.StartAsync(true, false, configureCookie: options =>
            options.Events.OnValidatePrincipal = context =>
            {
                calls++;
                context.ShouldRenew = true;
                return Task.CompletedTask;
            });
        var invitation = await fixture.BindAsync("google", "google-subject");
        var cookie = await fixture.SignInAsync("google", "google-subject");
        Assert.True(await fixture.Store.RevokeInvitationAsync(invitation));
        using var denied = await fixture.GetAsync("/api/auth/me", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Contains(denied.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(CookieName + "=;", StringComparison.Ordinal));
        Assert.True(calls > 0);
    }

    [Fact]
    public async Task Prior_rejection_is_preserved()
    {
        await using var fixture = await Fixture.StartAsync(true, false, configureCookie: options =>
            options.Events.OnValidatePrincipal = context =>
            {
                context.RejectPrincipal();
                return Task.CompletedTask;
            });
        await fixture.BindAsync("google", "google-subject");
        var cookie = await fixture.SignInAsync("google", "google-subject");
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prior_delegate_cannot_reclassify_protected_cookie_as_keycloak(bool legacy)
    {
        await using var fixture = await Fixture.StartAsync(true, false, configureCookie: options =>
            options.Events.OnValidatePrincipal = context =>
            {
                context.Properties.Items[LoginProviderKey] = "oidc";
                context.Properties.Items.Remove("Backend.InvitationSession.Provider");
                context.Properties.Items.Remove("Backend.InvitationSession.Subject");
                return Task.CompletedTask;
            });
        await fixture.BindAsync("google", "google-subject");
        var cookie = await fixture.SignInAsync("google", "google-subject", mark: !legacy);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    [Fact]
    public void Cookie_events_type_fails_fast_only_for_the_cookie_scheme()
    {
        var postConfigure = new InvitationCookiePostConfigure(
            new SqliteInvitationStore(Path.Combine(Path.GetTempPath(), $"invitation-{Guid.NewGuid():N}.db")),
            true, true);
        var other = new CookieAuthenticationOptions { EventsType = typeof(CookieAuthenticationEvents) };
        postConfigure.PostConfigure("other", other);
        Assert.NotNull(other.EventsType);
        var cookies = new CookieAuthenticationOptions { EventsType = typeof(CookieAuthenticationEvents) };
        Assert.Throws<InvalidOperationException>(() => postConfigure.PostConfigure("Cookies", cookies));
    }

    [Fact]
    public async Task Authentications_in_one_request_share_the_checked_ticket_but_next_request_rechecks()
    {
        await using var fixture = await Fixture.StartAsync(true, false);
        var invitation = await fixture.BindAsync("google", "google-subject");
        var cookie = await fixture.SignInAsync("google", "google-subject");
        using var during = await fixture.GetAsync($"/test/revoke/{invitation:N}", cookie);
        Assert.Equal(HttpStatusCode.OK, during.StatusCode);
        Assert.Equal("True,True", await during.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", cookie)).StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private sealed class RemoteCalls
        {
            public int SignOut;
            public int Challenge;
        }

        private readonly WebApplication app;
        private readonly HttpClient client;
        private readonly RemoteCalls remoteCalls;
        public string Path { get; }
        public SqliteInvitationStore Store { get; }
        public int RemoteSignOutCalls => remoteCalls.SignOut;
        public int RemoteChallengeCalls => remoteCalls.Challenge;

        private Fixture(WebApplication app, string path, RemoteCalls remoteCalls)
        {
            this.app = app;
            this.remoteCalls = remoteCalls;
            Path = path;
            Store = new SqliteInvitationStore(path);
            client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");
        }

        public static async Task<Fixture> StartAsync(bool google, bool github, bool initialize = true,
            Action<CookieAuthenticationOptions>? configureCookie = null)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"session-{Guid.NewGuid():N}.db");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:Authority"] = "https://oidc.example.test",
                ["Oidc:ClientId"] = "oidc-client",
                ["Oidc:ClientSecret"] = "oidc-secret"
            });
            builder.Services.AddOidcStarterBff(builder.Configuration);
            builder.Services.AddOidcStarterLoginProvider("google", "Google", "OidcStarter.Google");
            builder.Services.AddOidcStarterLoginProvider("github", "GitHub", "OidcStarter.GitHub");
            var remoteCalls = new RemoteCalls();
            builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = "https://oidc.example.test",
                    AuthorizationEndpoint = "https://oidc.example.test/authorize",
                    EndSessionEndpoint = "https://oidc.example.test/logout"
                };
                options.Events.OnRedirectToIdentityProviderForSignOut = context =>
                {
                    remoteCalls.SignOut++;
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    remoteCalls.Challenge++;
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
            if (configureCookie is not null)
                builder.Services.Configure(CookieAuthenticationDefaults.AuthenticationScheme, configureCookie);
            builder.Services.AddInvitationSessionValidation(path, google, github);
            var app = builder.Build();
            app.UseOidcStarterBff();
            app.MapControllers();
            app.MapGet("/test/signin", async (HttpContext context) =>
            {
                var query = context.Request.Query;
                var provider = query["provider"].ToString();
                var subject = query["subject"].ToString();
                var properties = new AuthenticationProperties();
                if (!string.IsNullOrEmpty(provider)) properties.Items[LoginProviderKey] = provider;
                if (query["mark"] != "false")
                    InvitationSessionValidation.Mark(properties,
                        query["markedProvider"].ToString() is { Length: > 0 } markedProvider
                            ? markedProvider : provider,
                        query["markedSubject"].ToString() is { Length: > 0 } marked ? marked : subject);
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, subject), new(ClaimTypes.Name, "Test User")
                };
                if (query["extraSubject"].ToString() is { Length: > 0 } extra)
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, extra));
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), properties);
            });
            var cookiePolicy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser().Build();
            app.MapGet("/api/test/protected", () => Results.Ok()).RequireAuthorization(cookiePolicy);
            app.MapPost("/api/test/protected", () => Results.Ok()).RequireAuthorization(cookiePolicy);
            app.MapPost("/api/test/other", () => Results.Ok());
            app.MapGet("/test/revoke/{id:guid}", async (HttpContext context, Guid id) =>
            {
                var store = context.RequestServices.GetRequiredService<SqliteInvitationStore>();
                await store.RevokeInvitationAsync(id);
                var first = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                var second = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Text($"{first.Succeeded},{second.Succeeded}");
            }).RequireAuthorization(cookiePolicy);
            await app.StartAsync();
            var fixture = new Fixture(app, path, remoteCalls);
            if (initialize) await fixture.Store.InitializeAsync();
            return fixture;
        }

        public async Task<Guid> BindAsync(string provider, string subject)
        {
            var invitation = await Store.CreateInvitationAsync($"{provider}-{subject}@example.test");
            Assert.True(await Store.ApproveInvitationAsync(invitation));
            var result = await Store.BindVerifiedIdentityAsync(provider, subject,
                $"{provider}-{subject}@example.test");
            Assert.True(result.Allowed);
            return invitation;
        }

        public async Task<string> SignInAsync(string provider, string subject, bool mark = true,
            string? markedSubject = null, string? extraSubject = null, string? markedProvider = null)
        {
            var url = $"/test/signin?provider={Uri.EscapeDataString(provider)}" +
                $"&subject={Uri.EscapeDataString(subject)}&mark={mark.ToString().ToLowerInvariant()}" +
                $"&markedSubject={Uri.EscapeDataString(markedSubject ?? string.Empty)}" +
                $"&markedProvider={Uri.EscapeDataString(markedProvider ?? string.Empty)}" +
                $"&extraSubject={Uri.EscapeDataString(extraSubject ?? string.Empty)}";
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Assert.Single(response.Headers.GetValues("Set-Cookie"),
                value => value.StartsWith(CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        }

        public Task<HttpResponseMessage> GetAsync(string path, string? cookie = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (cookie is not null) request.Headers.TryAddWithoutValidation("Cookie", cookie);
            return client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PostAsync(string path, string cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
            return client.SendAsync(request);
        }

        public async Task<(string RequestToken, string Cookie)> GetCsrfAsync(string? cookie = null)
        {
            using var response = await GetAsync("/api/auth/csrf", cookie);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
            var requestCookie = Assert.Single(cookies,
                value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)).Split(';')[0];
            var antiforgeryCookie = Assert.Single(cookies,
                value => value.StartsWith("__Host-oidc-starter-bff-af=", StringComparison.Ordinal)).Split(';')[0];
            return (Uri.UnescapeDataString(requestCookie["XSRF-TOKEN=".Length..]), antiforgeryCookie);
        }

        public Task<HttpResponseMessage> PostLogoutAsync(string cookie, (string RequestToken, string Cookie) csrf)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookie}; {csrf.Cookie}");
            request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", csrf.RequestToken);
            request.Headers.TryAddWithoutValidation("Origin", "https://localhost");
            return client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
            File.Delete(Path);
        }
    }
}
