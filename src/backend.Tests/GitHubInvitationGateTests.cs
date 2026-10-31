using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Backend.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace Backend.Tests;

public sealed class GitHubInvitationGateTests
{
    private const string VerifiedEmail = """[{"email":"person@example.test","primary":true,"verified":true}]""";

    [Theory]
    [InlineData("""{"id":123}""", "123")]
    [InlineData("""{"id":0}""", null)]
    [InlineData("""{"id":-1}""", null)]
    [InlineData("""{"id":1.5}""", null)]
    [InlineData("""{"id":"123"}""", null)]
    [InlineData("""{"id":123,"id":124}""", null)]
    [InlineData("""{"login":"octocat"}""", null)]
    [InlineData("""{"id":18446744073709551616}""", null)]
    public void Stable_numeric_id_must_be_unambiguous(string json, string? expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, GitHubInvitationGatePostConfigure.ReadSubject(document.RootElement));
    }

    [Theory]
    [InlineData(VerifiedEmail, "person@example.test")]
    [InlineData("""[{"email":"a@example.test","primary":false,"verified":true}]""", null)]
    [InlineData("""[{"email":"a@example.test","primary":true,"verified":false}]""", null)]
    [InlineData("""[{"email":"a@example.test","primary":true,"verified":true},{"email":"b@example.test","primary":true,"verified":true}]""", null)]
    [InlineData("""[{"email":"a@example.test","primary":true,"verified":"true"}]""", null)]
    [InlineData("""[{"email":"a@example.test","primary":1,"verified":true}]""", null)]
    [InlineData("""[{"primary":true,"verified":true}]""", null)]
    [InlineData("""[{"email":"a@example.test","email":"b@example.test","primary":true,"verified":true}]""", null)]
    [InlineData("""{"email":"a@example.test","primary":true,"verified":true}""", null)]
    [InlineData("[]", null)]
    public void Only_one_verified_primary_email_is_accepted(string json, string? expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, GitHubInvitationGatePostConfigure.ReadVerifiedPrimaryEmail(document.RootElement));
    }

    [Fact]
    public async Task First_binding_uses_verified_primary_not_public_email_and_returning_id_needs_no_email_call()
    {
        using var fixture = await Fixture.CreateAsync();
        var invitation = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        var fake = new FakeBackchannel(VerifiedEmail);
        using var client = new HttpClient(fake);
        var evidence = Evidence("123", "test-token", client);
        Assert.True(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence,
            Principal("123", "different-public@example.test"), fixture.Store));
        Assert.Equal(1, fake.EmailCalls);
        Assert.True(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence,
            Principal("123"), fixture.Store));
        Assert.Equal(1, fake.EmailCalls);
    }

    [Fact]
    public async Task Missing_revoked_and_unavailable_storage_denies()
    {
        using var fixture = await Fixture.CreateAsync();
        var fake = new FakeBackchannel(VerifiedEmail);
        using var client = new HttpClient(fake);
        var evidence = Evidence("123", "test-token", client);
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence, Principal("123"), fixture.Store));
        var id = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        Assert.True(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence, Principal("123"), fixture.Store));
        Assert.True(await fixture.Store.RevokeInvitationAsync(id));
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence, Principal("123"), fixture.Store));
        Assert.Equal(2, fake.EmailCalls);
        var missingPath = Path.Combine(Path.GetTempPath(), $"github-gate-{Guid.NewGuid():N}.db");
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence, Principal("123"),
            new SqliteInvitationStore(missingPath)));
        Assert.False(File.Exists(missingPath));
    }

    [Fact]
    public async Task Unapproved_and_expired_invitations_deny_first_binding()
    {
        using var fixture = await Fixture.CreateAsync();
        var fake = new FakeBackchannel(VerifiedEmail);
        using var client = new HttpClient(fake);
        var evidence = Evidence("123", "test-token", client);
        await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(evidence, Principal("123"), fixture.Store));
        var expiring = await fixture.Store.CreateInvitationAsync("expires@example.test",
            fixture.Clock.GetUtcNow().AddMinutes(1));
        Assert.True(await fixture.Store.ApproveInvitationAsync(expiring));
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        using var expiringClient = new HttpClient(new FakeBackchannel(
            """[{"email":"expires@example.test","primary":true,"verified":true}]"""));
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(
            Evidence("456", "test-token", expiringClient),
            Principal("456"), fixture.Store));
    }

    [Fact]
    public async Task Public_email_and_competing_first_bindings_cannot_bypass_verified_primary()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        using var unverifiedClient = new HttpClient(new FakeBackchannel(
            """[{"email":"person@example.test","primary":true,"verified":false}]"""));
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(
            Evidence("123", "test-token", unverifiedClient),
            Principal("123", "person@example.test"), fixture.Store));
        var attempts = Enumerable.Range(1, 8).Select(async number =>
        {
            using var client = new HttpClient(new FakeBackchannel(VerifiedEmail));
            return await GitHubInvitationGatePostConfigure.IsAllowedAsync(
                Evidence(number.ToString(), "test-token", client),
                Principal(number.ToString()), fixture.Store);
        });
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(static allowed => allowed));
    }

    [Fact]
    public async Task Principal_mismatch_and_missing_token_deny()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        var fake = new FakeBackchannel(VerifiedEmail);
        using var client = new HttpClient(fake);
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(
            Evidence("123", "test-token", client),
            Principal("456"), fixture.Store));
        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(
            Evidence("123", null, client),
            Principal("123"), fixture.Store));
        Assert.Equal(0, fake.EmailCalls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Email_api_errors_deny(HttpStatusCode status)
    {
        using var client = new HttpClient(new FakeBackchannel(VerifiedEmail, status));
        var evidence = Evidence("123", "test-token", client);
        Assert.Null(await GitHubInvitationGatePostConfigure.GetVerifiedPrimaryEmailAsync(evidence,
            CancellationToken.None));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task Malformed_or_incomplete_email_api_response_denies(string body)
    {
        using var client = new HttpClient(new FakeBackchannel(body));
        Assert.Null(await GitHubInvitationGatePostConfigure.GetVerifiedPrimaryEmailAsync(
            Evidence("123", "test-token", client),
            CancellationToken.None));
    }

    [Fact]
    public void Google_and_GitHub_gates_share_one_store_registration()
    {
        var services = new ServiceCollection();
        var path = Path.Combine(Path.GetTempPath(), $"github-gate-{Guid.NewGuid():N}.db");
        services.AddGoogleInvitationGate(path);
        services.AddGitHubInvitationGate(path);
        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<SqliteInvitationStore>());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Enforced_options_disable_implicit_email_fallback_and_reject_EventsType()
    {
        using var fixture = await Fixture.CreateAsync();
        var gate = new GitHubInvitationGatePostConfigure(fixture.Store);
        var options = new GitHubAuthenticationOptions();
        gate.PostConfigure("OidcStarter.GitHub", options);
        Assert.Equal(string.Empty, options.UserEmailsEndpoint);
        var typed = new GitHubAuthenticationOptions { EventsType = typeof(Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents) };
        Assert.Throws<InvalidOperationException>(() => gate.PostConfigure("OidcStarter.GitHub", typed));
    }

    [Fact]
    public async Task Enterprise_email_request_stays_on_the_effective_enterprise_API_origin()
    {
        using var fixture = await Fixture.CreateAsync();
        var options = new GitHubAuthenticationOptions { EnterpriseDomain = "https://ghe.example.test" };
        new GitHubPostConfigureOptions().PostConfigure("OidcStarter.GitHub", options);
        var endpoint = GitHubInvitationGatePostConfigure.ResolveTrustedEmailsEndpoint(options);
        Assert.Equal("https://ghe.example.test/api/v3/user/emails", endpoint?.AbsoluteUri);
        new GitHubInvitationGatePostConfigure(fixture.Store).PostConfigure("OidcStarter.GitHub", options);
        Assert.Equal(string.Empty, options.UserEmailsEndpoint);
        var fake = new FakeBackchannel(VerifiedEmail);
        using var client = new HttpClient(fake);
        Assert.Equal("person@example.test", await GitHubInvitationGatePostConfigure.GetVerifiedPrimaryEmailAsync(
            new GitHubInvitationGatePostConfigure.Evidence("123", "test-token", client, endpoint!),
            CancellationToken.None));
        Assert.Equal("ghe.example.test", fake.LastEmailRequestHost);
        Assert.Equal("Bearer", fake.LastEmailAuthorizationScheme);
    }

    [Theory]
    [InlineData("https://unrelated.example.test/user/emails")]
    [InlineData("https://api.github.com/other")]
    [InlineData("https://api.github.com/user/emails?unexpected=1")]
    [InlineData("https://api.github.com/user/emails#fragment")]
    public async Task Verified_looking_email_from_redirected_endpoint_never_binds(string finalUri)
    {
        using var fixture = await Fixture.CreateAsync();
        var invitation = await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        var fake = new FakeBackchannel(VerifiedEmail, finalEmailUri: finalUri);
        using var client = new HttpClient(fake);

        Assert.False(await GitHubInvitationGatePostConfigure.IsAllowedAsync(
            Evidence("123", "test-token", client), Principal("123"), fixture.Store));

        Assert.Equal(1, fake.EmailCalls);
        Assert.Equal(InvitationDecisionReason.BindingMissing,
            (await fixture.Store.CheckBoundIdentityAsync("github", "123")).Reason);
    }

    [Fact]
    public async Task Missing_final_request_URI_is_not_accepted_as_email_evidence()
    {
        using var client = new HttpClient(new FakeBackchannel(VerifiedEmail, omitFinalRequest: true));
        Assert.Null(await GitHubInvitationGatePostConfigure.GetVerifiedPrimaryEmailAsync(
            Evidence("123", "test-token", client), CancellationToken.None));
    }

    [Theory]
    [InlineData("UserEmailsEndpoint", "https://unrelated.example.test/user/emails")]
    [InlineData("UserEmailsEndpoint", "http://api.github.com/user/emails")]
    [InlineData("UserEmailsEndpoint", "https://user@api.github.com/user/emails")]
    [InlineData("UserEmailsEndpoint", "https://api.github.com/user/emails#fragment")]
    [InlineData("UserInformationEndpoint", "https://unrelated.example.test/user")]
    [InlineData("TokenEndpoint", "https://unrelated.example.test/login/oauth/access_token")]
    [InlineData("AuthorizationEndpoint", "http://github.com/login/oauth/authorize")]
    public async Task Untrusted_endpoint_topology_is_rejected_before_any_email_request(string property, string value)
    {
        using var fixture = await Fixture.CreateAsync();
        var options = new GitHubAuthenticationOptions();
        switch (property)
        {
            case "UserEmailsEndpoint": options.UserEmailsEndpoint = value; break;
            case "UserInformationEndpoint": options.UserInformationEndpoint = value; break;
            case "TokenEndpoint": options.TokenEndpoint = value; break;
            case "AuthorizationEndpoint": options.AuthorizationEndpoint = value; break;
        }
        Assert.Null(GitHubInvitationGatePostConfigure.ResolveTrustedEmailsEndpoint(options));
        Assert.Throws<InvalidOperationException>(() =>
            new GitHubInvitationGatePostConfigure(fixture.Store).PostConfigure("OidcStarter.GitHub", options));
    }

    [Theory]
    [InlineData("http://ghe.example.test")]
    [InlineData("https://ghe.example.test/path")]
    [InlineData("https://user@ghe.example.test")]
    [InlineData("https://ghe.example.test#fragment")]
    public async Task Unsafe_enterprise_domain_is_rejected(string domain)
    {
        using var fixture = await Fixture.CreateAsync();
        var options = new GitHubAuthenticationOptions { EnterpriseDomain = domain };
        new GitHubPostConfigureOptions().PostConfigure("OidcStarter.GitHub", options);
        Assert.Null(GitHubInvitationGatePostConfigure.ResolveTrustedEmailsEndpoint(options));
        Assert.Throws<InvalidOperationException>(() =>
            new GitHubInvitationGatePostConfigure(fixture.Store).PostConfigure("OidcStarter.GitHub", options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prior_ticket_Success_still_requires_invitation(bool approved)
    {
        using var fixture = await Fixture.CreateAsync();
        if (approved)
        {
            var id = await fixture.Store.CreateInvitationAsync("person@example.test");
            Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        }
        var fake = new FakeBackchannel(VerifiedEmail);
        using var backchannel = new HttpClient(fake);
        var options = new GitHubAuthenticationOptions();
        options.Events.OnTicketReceived = context =>
        {
            context.Success();
            return Task.CompletedTask;
        };
        new GitHubInvitationGatePostConfigure(fixture.Store).PostConfigure("OidcStarter.GitHub", options);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var scheme = new AuthenticationScheme("OidcStarter.GitHub", "GitHub", typeof(GitHubAuthenticationHandler));
        var properties = new AuthenticationProperties();
        var principal = Principal("123");
        using var user = JsonDocument.Parse("""{"id":123,"login":"octocat","email":null}""");
        using var tokenJson = JsonDocument.Parse("""{"access_token":"test-token"}""");
        var creating = new OAuthCreatingTicketContext(principal, properties, http, scheme, options,
            backchannel, OAuthTokenResponse.Success(tokenJson), user.RootElement);
        await options.Events.OnCreatingTicket(creating);
        var received = new TicketReceivedContext(http, scheme, options,
            new AuthenticationTicket(principal, properties, scheme.Name));

        await options.Events.OnTicketReceived(received);

        if (approved)
        {
            Assert.False(received.Result?.Handled ?? false);
            Assert.Equal(InvitationDecisionReason.BoundIdentity,
                (await fixture.Store.CheckBoundIdentityAsync("github", "123")).Reason);
        }
        else
        {
            Assert.True(received.Result?.Handled);
            Assert.Equal("/?login=unavailable", http.Response.Headers.Location.ToString());
            Assert.Equal(0, http.Response.Headers.SetCookie.Count);
        }
        Assert.Equal(1, fake.EmailCalls);
        Assert.DoesNotContain(properties.Items, static item => item.Value?.Contains("test-token") == true);
    }

    [Theory]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, true, false, false)]
    public async Task Real_github_callback_controls_session_cookie(
        bool approved, bool verified, bool privateEmail, bool enforced, bool prebound)
    {
        using var fixture = await Fixture.CreateAsync();
        if (approved)
        {
            var id = await fixture.Store.CreateInvitationAsync("person@example.test");
            Assert.True(await fixture.Store.ApproveInvitationAsync(id));
            if (prebound)
                Assert.True((await fixture.Store.BindVerifiedIdentityAsync("github", "123", "person@example.test")).Allowed);
        }
        var emailJson = verified ? VerifiedEmail :
            """[{"email":"person@example.test","primary":true,"verified":false}]""";
        var fake = new FakeBackchannel(emailJson, profileEmail: privateEmail ? null : "public@example.test");
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Oidc:Authority"] = "https://oidc.example.test",
            ["Oidc:ClientId"] = "oidc-client",
            ["Oidc:ClientSecret"] = "oidc-secret"
        });
        builder.Services.AddOidcStarterBff(builder.Configuration);
        builder.Services.AddOidcStarterGitHub(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GitHub:ClientId"] = "github-client",
                ["GitHub:ClientSecret"] = "github-secret"
            }).Build().GetSection("GitHub"));
        var tokenPersisted = false;
        builder.Services.Configure<GitHubAuthenticationOptions>("OidcStarter.GitHub", options =>
        {
            options.TokenEndpoint = GitHubAuthenticationDefaults.TokenEndpoint;
            options.UserInformationEndpoint = "https://api.github.com/user";
            options.BackchannelHttpHandler = fake;
            options.Events.OnTicketReceived = context =>
            {
                tokenPersisted = context.Properties?.GetTokenValue("access_token") is not null;
                return Task.CompletedTask;
            };
        });
        if (enforced) builder.Services.AddGitHubInvitationGate(fixture.Path);
        await using var app = builder.Build();
        var actualOptions = app.Services.GetRequiredService<IOptionsMonitor<GitHubAuthenticationOptions>>()
            .Get("OidcStarter.GitHub");
        Assert.Contains("user:email", actualOptions.Scope);
        Assert.True(actualOptions.UsePkce);
        Assert.False(actualOptions.SaveTokens);
        Assert.Equal(enforced ? string.Empty : GitHubAuthenticationDefaults.UserEmailsEndpoint,
            actualOptions.UserEmailsEndpoint);
        app.UseOidcStarterBff();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var challenge = await client.GetAsync("/api/auth/login/github");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var state = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"].ToString();
        var correlation = Assert.Single(challenge.Headers.GetValues("Set-Cookie"),
            static header => header.StartsWith(".AspNetCore.Correlation.", StringComparison.Ordinal));
        using var callback = new HttpRequestMessage(HttpMethod.Get,
            $"/signin-github?code=test-code&state={Uri.EscapeDataString(state)}");
        callback.Headers.TryAddWithoutValidation("Cookie", correlation.Split(';')[0]);
        using var response = await client.SendAsync(callback);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(static cookie => cookie.Split('=')[0]).ToArray() : [];

        Assert.False(tokenPersisted);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        if (enforced && approved && (verified || prebound))
        {
            Assert.Contains("__Host-oidc-starter-bff", cookies);
            Assert.Equal(prebound ? 0 : 1, fake.EmailCalls);
        }
        else if (!enforced)
        {
            Assert.Contains("__Host-oidc-starter-bff", cookies);
            Assert.Equal(privateEmail ? 1 : 0, fake.EmailCalls);
        }
        else
        {
            Assert.Equal("/?login=unavailable", response.Headers.Location?.OriginalString);
            Assert.DoesNotContain("__Host-oidc-starter-bff", cookies);
            Assert.Equal(1, fake.EmailCalls);
        }
    }

    private static ClaimsPrincipal Principal(string subject, string? email = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };
        if (email is not null) claims.Add(new Claim(ClaimTypes.Email, email));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "GitHub"));
    }

    private static GitHubInvitationGatePostConfigure.Evidence Evidence(
        string? subject, string? token, HttpClient backchannel)
        => new(subject, token, backchannel, new Uri(GitHubAuthenticationDefaults.UserEmailsEndpoint));

    private sealed class FakeBackchannel(string emailJson, HttpStatusCode emailStatus = HttpStatusCode.OK,
        string? profileEmail = "public@example.test", string? finalEmailUri = null,
        bool omitFinalRequest = false) : HttpMessageHandler
    {
        public int EmailCalls { get; private set; }
        public string? LastEmailRequestHost { get; private set; }
        public string? LastEmailAuthorizationScheme { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath;
            if (path?.EndsWith("/user/emails", StringComparison.Ordinal) == true)
            {
                EmailCalls++;
                LastEmailRequestHost = request.RequestUri?.Host;
                LastEmailAuthorizationScheme = request.Headers.Authorization?.Scheme;
            }
            var body = path switch
            {
                "/login/oauth/access_token" => """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""",
                "/user" => $$"""{"id":123,"login":"octocat","email":{{JsonSerializer.Serialize(profileEmail)}}}""",
                "/user/emails" => emailJson,
                "/api/v3/user/emails" => emailJson,
                _ => throw new InvalidOperationException("Unexpected backchannel request")
            };
            if (path?.EndsWith("/user/emails", StringComparison.Ordinal) == true && finalEmailUri is not null)
                request.RequestUri = new Uri(finalEmailUri);
            return Task.FromResult(new HttpResponseMessage(path?.EndsWith("/user/emails", StringComparison.Ordinal) == true
                ? emailStatus : HttpStatusCode.OK)
            {
                RequestMessage = omitFinalRequest ? null : request,
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
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
            var fixture = new Fixture(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"github-gate-{Guid.NewGuid():N}.db"));
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
