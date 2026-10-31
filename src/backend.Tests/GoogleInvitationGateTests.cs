using System.Security.Claims;
using System.Text.Json;
using Backend.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace Backend.Tests;

public sealed class GoogleInvitationGateTests
{
    [Fact]
    public async Task Approved_verified_identity_binds_and_returning_subject_survives_email_change()
    {
        using var fixture = await Fixture.CreateAsync();
        var invitation = await fixture.Store.CreateInvitationAsync("first@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));

        Assert.True(await Allowed(fixture.Store, "subject-1", "first@example.test", "true"));
        Assert.True(await Allowed(fixture.Store, "subject-1", "changed@example.test", "false"));
        Assert.Equal(InvitationDecisionReason.BoundIdentity,
            (await fixture.Store.CheckBoundIdentityAsync("google", "subject-1")).Reason);
        Assert.False(await Allowed(fixture.Store, "subject-2", "first@example.test", "true"));
    }

    [Fact]
    public async Task Unapproved_expired_revoked_and_missing_invitations_are_denied()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.Store.CreateInvitationAsync("pending@example.test");
        Assert.False(await Allowed(fixture.Store, "pending", "pending@example.test", "true"));

        var expired = await fixture.Store.CreateInvitationAsync("expired@example.test",
            fixture.Clock.GetUtcNow().AddMinutes(1));
        Assert.True(await fixture.Store.ApproveInvitationAsync(expired));
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.False(await Allowed(fixture.Store, "expired", "expired@example.test", "true"));

        var revoked = await fixture.Store.CreateInvitationAsync("revoked@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(revoked));
        Assert.True(await Allowed(fixture.Store, "revoked", "revoked@example.test", "true"));
        Assert.True(await fixture.Store.RevokeInvitationAsync(revoked));
        Assert.False(await Allowed(fixture.Store, "revoked", "revoked@example.test", "true"));
        Assert.False(await Allowed(fixture.Store, "missing", "missing@example.test", "true"));
    }

    [Theory]
    [InlineData("null", "person@example.test")]
    [InlineData("false", "person@example.test")]
    [InlineData("\"true\"", "person@example.test")]
    [InlineData("true", null)]
    public async Task First_binding_requires_valid_verified_provider_email(string verification, string? email)
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        Assert.False(await Allowed(fixture.Store, "subject", email, verification));
        Assert.Equal(InvitationDecisionReason.BindingMissing,
            (await fixture.Store.CheckBoundIdentityAsync("google", "subject")).Reason);
    }

    [Fact]
    public async Task Missing_or_malformed_subject_and_principal_mismatch_are_denied()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        Assert.False(await Allowed(fixture.Store, null, "person@example.test", "true"));
        Assert.False(await Allowed(fixture.Store, " ", "person@example.test", "true"));
        Assert.False(await Allowed(fixture.Store, "subject", "person@example.test", "true", "other"));
        Assert.False(await Allowed(fixture.Store, "subject", "person@example.test", "true",
            principalEmail: "other@example.test"));
    }

    [Theory]
    [InlineData("\"sub\": \"other\",")]
    [InlineData("\"email\": \"other@example.test\",")]
    [InlineData("\"email_verified\": false,")]
    public void Duplicate_provider_evidence_is_rejected(string duplicate)
    {
        using var profile = JsonDocument.Parse($$"""
            { "sub": "subject", "email": "person@example.test", "email_verified": true,
              {{duplicate}} "locale": "en" }
            """);
        Assert.Null(GoogleInvitationGatePostConfigure.ReadEvidence(profile.RootElement));
    }

    [Fact]
    public async Task Missing_database_denies_without_creating_it()
    {
        var path = Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db");
        var store = new SqliteInvitationStore(path);
        Assert.False(await Allowed(store, "subject", "person@example.test", "true"));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Competing_first_bindings_have_one_winner()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        var attempts = Enumerable.Range(0, 8).Select(i =>
            Allowed(fixture.Store, $"subject-{i}", "person@example.test", "true"));
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(static allowed => allowed));
    }

    [Fact]
    public void EventsType_is_rejected_and_other_schemes_are_unchanged()
    {
        var gate = new GoogleInvitationGatePostConfigure(new SqliteInvitationStore(
            Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db")));
        var other = new GoogleOptions();
        gate.PostConfigure("other", other);
        Assert.Null(other.EventsType);

        var google = new GoogleOptions { EventsType = typeof(OAuthEvents) };
        Assert.Throws<InvalidOperationException>(() => gate.PostConfigure("OidcStarter.Google", google));
    }

    [Fact]
    public void Enabled_gate_requires_an_absolute_path_without_creating_a_database()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddGoogleInvitationGate(null));
        Assert.Throws<ArgumentException>(() => services.AddGoogleInvitationGate("relative.db"));
        var path = Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db");
        services.AddGoogleInvitationGate(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Ticket_denial_handles_response_with_fixed_redirect_and_preserves_prior_event()
    {
        var previousCalled = false;
        var options = new GoogleOptions();
        options.Events.OnTicketReceived = _ =>
        {
            previousCalled = true;
            return Task.CompletedTask;
        };
        var gate = new GoogleInvitationGatePostConfigure(new SqliteInvitationStore(
            Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db")));
        gate.PostConfigure("OidcStarter.Google", options);
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?returnUrl=https://evil.example.test");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "sensitive-subject")], "Google"));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), "OidcStarter.Google");
        var context = new TicketReceivedContext(http,
            new AuthenticationScheme("OidcStarter.Google", "Google", typeof(GoogleHandler)), options, ticket);

        await options.Events.OnTicketReceived(context);

        Assert.True(previousCalled);
        Assert.True(context.Result?.Handled);
        Assert.Equal("/?login=unavailable", http.Response.Headers.Location.ToString());
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        Assert.DoesNotContain("sensitive-subject", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Ticket_events_compose_and_bind_from_transient_google_profile()
    {
        using var fixture = await Fixture.CreateAsync();
        var invitation = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        var creatingCalled = false;
        var receivedCalled = false;
        var options = new GoogleOptions();
        options.Events.OnCreatingTicket = _ =>
        {
            creatingCalled = true;
            return Task.CompletedTask;
        };
        options.Events.OnTicketReceived = _ =>
        {
            receivedCalled = true;
            return Task.CompletedTask;
        };
        new GoogleInvitationGatePostConfigure(fixture.Store).PostConfigure("OidcStarter.Google", options);
        var http = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("OidcStarter.Google", "Google", typeof(GoogleHandler));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "google-subject"),
            new Claim(ClaimTypes.Email, "person@example.test")
        ], "Google"));
        var properties = new AuthenticationProperties();
        using var profile = JsonDocument.Parse("""
            { "sub": "google-subject", "email": "person@example.test", "email_verified": true }
            """);
        using var tokenJson = JsonDocument.Parse("""{ "access_token": "test-token" }""");
        var tokens = OAuthTokenResponse.Success(tokenJson);
        var creating = new OAuthCreatingTicketContext(principal, properties, http, scheme, options,
            new HttpClient(), tokens, profile.RootElement);

        await options.Events.OnCreatingTicket(creating);
        var received = new TicketReceivedContext(http, scheme, options,
            new AuthenticationTicket(principal, properties, scheme.Name));
        await options.Events.OnTicketReceived(received);

        Assert.True(creatingCalled);
        Assert.True(receivedCalled);
        Assert.Null(received.Result);
        Assert.Equal(InvitationDecisionReason.BoundIdentity,
            (await fixture.Store.CheckBoundIdentityAsync("google", "google-subject")).Reason);
        Assert.DoesNotContain(properties.Items, static item => item.Value?.Contains("person@example.test") == true);
    }

    [Theory]
    [InlineData("Success", false, false)]
    [InlineData("Success", true, true)]
    [InlineData("Fail", true, false)]
    [InlineData("Handled", true, null)]
    [InlineData("Skipped", true, null)]
    public async Task Prior_ticket_result_cannot_bypass_gate(string result, bool approved, bool? expectedAllow)
    {
        using var fixture = await Fixture.CreateAsync();
        if (approved)
        {
            var invitation = await fixture.Store.CreateInvitationAsync("person@example.test");
            Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        }

        var options = new GoogleOptions();
        options.Events.OnTicketReceived = context =>
        {
            switch (result)
            {
                case "Success": context.Success(); break;
                case "Fail": context.Fail("sensitive-failure"); break;
                case "Handled": context.HandleResponse(); break;
                case "Skipped": context.SkipHandler(); break;
            }
            return Task.CompletedTask;
        };
        new GoogleInvitationGatePostConfigure(fixture.Store).PostConfigure("OidcStarter.Google", options);
        var http = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("OidcStarter.Google", "Google", typeof(GoogleHandler));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "subject"),
            new Claim(ClaimTypes.Email, "person@example.test")
        ], "Google"));
        var properties = new AuthenticationProperties();
        using var profile = JsonDocument.Parse("""
            { "sub": "subject", "email": "person@example.test", "email_verified": true }
            """);
        using var tokenJson = JsonDocument.Parse("""{ "access_token": "test-token" }""");
        var creating = new OAuthCreatingTicketContext(principal, properties, http, scheme, options,
            new HttpClient(), OAuthTokenResponse.Success(tokenJson), profile.RootElement);
        await options.Events.OnCreatingTicket(creating);
        var received = new TicketReceivedContext(http, scheme, options,
            new AuthenticationTicket(principal, properties, scheme.Name));

        await options.Events.OnTicketReceived(received);

        if (expectedAllow is true)
        {
            Assert.False(received.Result?.Handled ?? false);
            Assert.Equal(InvitationDecisionReason.BoundIdentity,
                (await fixture.Store.CheckBoundIdentityAsync("google", "subject")).Reason);
        }
        else if (expectedAllow is false)
        {
            Assert.True(received.Result?.Handled);
            Assert.Equal("/?login=unavailable", http.Response.Headers.Location.ToString());
            Assert.Equal(0, http.Response.Headers.SetCookie.Count);
            Assert.Equal(InvitationDecisionReason.BindingMissing,
                (await fixture.Store.CheckBoundIdentityAsync("google", "subject")).Reason);
        }
        else
        {
            Assert.True(received.Result is { Handled: true } or { Skipped: true });
            Assert.Equal(InvitationDecisionReason.BindingMissing,
                (await fixture.Store.CheckBoundIdentityAsync("google", "subject")).Reason);
        }
    }

    [Fact]
    public async Task Remote_failure_uses_the_same_fixed_generic_redirect()
    {
        var options = new GoogleOptions();
        var previousCalled = false;
        options.Events.OnRemoteFailure = _ =>
        {
            previousCalled = true;
            return Task.CompletedTask;
        };
        var gate = new GoogleInvitationGatePostConfigure(new SqliteInvitationStore(
            Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db")));
        gate.PostConfigure("OidcStarter.Google", options);
        var http = new DefaultHttpContext();
        var context = new RemoteFailureContext(http,
            new AuthenticationScheme("OidcStarter.Google", "Google", typeof(GoogleHandler)),
            options, new InvalidOperationException("sensitive-provider-error"));

        await options.Events.OnRemoteFailure(context);

        Assert.True(previousCalled);
        Assert.True(context.Result?.Handled);
        Assert.Equal("/?login=unavailable", http.Response.Headers.Location.ToString());
        Assert.DoesNotContain("sensitive-provider-error", http.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Prior_remote_failure_remains_generic_after_failure_is_replaced()
    {
        var options = new GoogleOptions();
        options.Events.OnRemoteFailure = context =>
        {
            context.Failure = new InvalidOperationException("sensitive-prior-failure");
            return Task.CompletedTask;
        };
        new GoogleInvitationGatePostConfigure(new SqliteInvitationStore(
            Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db")))
            .PostConfigure("OidcStarter.Google", options);
        var http = new DefaultHttpContext();
        var context = new RemoteFailureContext(http,
            new AuthenticationScheme("OidcStarter.Google", "Google", typeof(GoogleHandler)), options,
            new InvalidOperationException("sensitive-provider-error"));

        await options.Events.OnRemoteFailure(context);

        Assert.True(context.Result?.Handled);
        Assert.Equal("/?login=unavailable", http.Response.Headers.Location.ToString());
        Assert.DoesNotContain("sensitive", http.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Explicitly_handled_or_skipped_remote_failure_is_preserved(bool handled)
    {
        var options = new GoogleOptions();
        options.Events.OnRemoteFailure = context =>
        {
            if (handled) context.HandleResponse();
            else context.SkipHandler();
            return Task.CompletedTask;
        };
        new GoogleInvitationGatePostConfigure(new SqliteInvitationStore(
            Path.Combine(Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db")))
            .PostConfigure("OidcStarter.Google", options);
        var http = new DefaultHttpContext();
        var context = new RemoteFailureContext(http,
            new AuthenticationScheme("OidcStarter.Google", "Google", typeof(GoogleHandler)), options,
            new InvalidOperationException("sensitive-provider-error"));

        await options.Events.OnRemoteFailure(context);

        Assert.Equal(handled, context.Result?.Handled);
        Assert.Equal(!handled, context.Result?.Skipped);
        Assert.False(http.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_google_callback_issues_a_session_only_for_an_approved_identity(bool approved)
    {
        using var fixture = await Fixture.CreateAsync();
        if (approved)
        {
            var invitation = await fixture.Store.CreateInvitationAsync("person@example.test");
            Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Oidc:Authority"] = "https://oidc.example.test",
            ["Oidc:ClientId"] = "oidc-client",
            ["Oidc:ClientSecret"] = "oidc-secret"
        });
        builder.Services.AddOidcStarterBff(builder.Configuration);
        builder.Services.AddOidcStarterGoogle(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "google-client",
                ["Google:ClientSecret"] = "google-secret"
            }).Build().GetSection("Google"));
        var creatingReached = false;
        var ticketReached = false;
        string? callbackFailure = null;
        var fakeBackchannel = new FakeGoogleBackchannel();
        builder.Services.Configure<GoogleOptions>("OidcStarter.Google", options =>
        {
            options.TokenEndpoint = "https://google.example.test/token";
            options.UserInformationEndpoint = "https://google.example.test/userinfo";
            options.BackchannelHttpHandler = fakeBackchannel;
            options.Events.OnCreatingTicket = _ =>
            {
                creatingReached = true;
                return Task.CompletedTask;
            };
            options.Events.OnTicketReceived = _ =>
            {
                ticketReached = true;
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                callbackFailure = context.Failure?.Message;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddGoogleInvitationGate(fixture.Path);
        await using var app = builder.Build();
        app.UseOidcStarterBff();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        // Exercise the public direct-login URL and its real Google callback handler.
        using var challenge = await client.GetAsync("/api/auth/login/google");
        Assert.Equal(System.Net.HttpStatusCode.Redirect, challenge.StatusCode);
        var state = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"].ToString();
        var correlation = Assert.Single(challenge.Headers.GetValues("Set-Cookie"),
            static header => header.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));
        using var callback = new HttpRequestMessage(HttpMethod.Get,
            $"/signin-google?code=test-code&state={Uri.EscapeDataString(state)}");
        callback.Headers.TryAddWithoutValidation("Cookie", correlation.Split(';')[0]);
        using var response = await client.SendAsync(callback);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToArray() : [];
        var cookieNames = cookies.Select(static cookie => cookie.Split('=')[0]).ToArray();
        Assert.Null(callbackFailure);
        Assert.Equal("/token,/userinfo", string.Join(',', fakeBackchannel.RequestPaths));
        Assert.True(creatingReached);
        Assert.True(ticketReached);

        if (approved)
        {
            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotEqual("/?login=unavailable", response.Headers.Location?.OriginalString);
            Assert.Contains("__Host-oidc-starter-bff", cookieNames);
        }
        else
        {
            Assert.Equal("/?login=unavailable", response.Headers.Location?.OriginalString);
            Assert.DoesNotContain(cookies, static cookie => cookie.StartsWith("__Host-oidc-starter-bff=", StringComparison.Ordinal));
        }
    }

    private sealed class FakeGoogleBackchannel : HttpMessageHandler
    {
        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri?.AbsolutePath ?? "missing-uri");
            var body = request.RequestUri?.AbsolutePath switch
            {
                "/token" => """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""",
                "/userinfo" => """{"sub":"google-subject","email":"person@example.test","email_verified":true,"name":"Google User"}""",
                _ => throw new InvalidOperationException("Unexpected backchannel request")
            };
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<bool> Allowed(SqliteInvitationStore store, string? subject, string? email,
        string? verification, string? principalSubject = null, string? principalEmail = null)
    {
        using var profile = JsonDocument.Parse($$"""
            { "sub": {{JsonSerializer.Serialize(subject)}},
              "email": {{JsonSerializer.Serialize(email)}},
              "email_verified": {{verification ?? "null"}} }
            """);
        var evidence = GoogleInvitationGatePostConfigure.ReadEvidence(profile.RootElement);
        var claims = new List<Claim>();
        if (subject is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, principalSubject ?? subject));
        }
        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, principalEmail ?? email));
        }
        return await GoogleInvitationGatePostConfigure.IsAllowedAsync(evidence,
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Google")), store);
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; }
        public SqliteInvitationStore Store { get; }
        public TestClock Clock { get; } = new();

        private Fixture(string path)
        {
            Path = path;
            Store = new SqliteInvitationStore(path, Clock);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"google-gate-{Guid.NewGuid():N}.db"));
            await fixture.Store.InitializeAsync();
            return fixture;
        }

        public void Dispose() => File.Delete(Path);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
