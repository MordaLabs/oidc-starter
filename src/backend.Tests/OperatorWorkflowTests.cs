using System.Net;
using System.Security.Claims;
using Backend.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace Backend.Tests;

public sealed class OperatorWorkflowTests
{
    private const string Issuer = "https://oidc.example.test/realm";
    private const string Subject = "operator-subject";
    private const string ClientId = "oidc-client";

    [Fact]
    public async Task Approval_is_atomic_and_links_one_approved_invitation()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = await fixture.RequestAsync("person@example.test");
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            fixture.Store.ApproveRequestAsync(id, "trusted-operator")));
        Assert.Single(results, result => result == OperatorMutationResult.Success);
        Assert.All(results.Where(result => result != OperatorMutationResult.Success),
            result => Assert.Equal(OperatorMutationResult.Conflict, result));
        Assert.Equal(1, await fixture.CountAsync("invitations"));
        var record = Assert.Single((await fixture.Store.ListInvitationRequestsAsync("approved"))!);
        Assert.Equal(id, record.Id);
        Assert.NotNull(record.InvitationId);
        Assert.Equal("trusted-operator", await fixture.ScalarAsync<string>(
            "SELECT decided_by FROM invitation_requests"));
        Assert.Equal(InvitationDecisionReason.NewBinding,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "google-sub", "person@example.test")).Reason);
    }

    [Fact]
    public async Task Approve_reject_race_has_one_winner_and_rejection_creates_no_invitation()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = await fixture.RequestAsync("race@example.test");
        var decisions = await Task.WhenAll(
            fixture.Store.ApproveRequestAsync(id, "operator"),
            fixture.Store.RejectRequestAsync(id, "operator"));
        Assert.Single(decisions, result => result == OperatorMutationResult.Success);
        Assert.Single(decisions, result => result == OperatorMutationResult.Conflict);
        var approved = (await fixture.Store.ListInvitationRequestsAsync("approved"))!;
        var rejected = (await fixture.Store.ListInvitationRequestsAsync("rejected"))!;
        Assert.Equal(1, approved.Count + rejected.Count);
        Assert.Equal(approved.Count, await fixture.CountAsync("invitations"));

        var rejectedId = await fixture.RequestAsync("reject@example.test");
        Assert.Equal(OperatorMutationResult.Success, await fixture.Store.RejectRequestAsync(rejectedId, "operator"));
        Assert.Equal(OperatorMutationResult.Conflict, await fixture.Store.RejectRequestAsync(rejectedId, "operator"));
        Assert.Equal(OperatorMutationResult.Conflict, await fixture.Store.ApproveRequestAsync(rejectedId, "operator"));
        Assert.Equal(approved.Count, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Existing_active_invitation_conflicts_without_changing_pending_request()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = await fixture.RequestAsync("person@example.test");
        await fixture.Store.CreateInvitationAsync("person@example.test");
        Assert.Equal(OperatorMutationResult.Conflict, await fixture.Store.ApproveRequestAsync(id, "operator"));
        Assert.Single((await fixture.Store.ListInvitationRequestsAsync("pending"))!);
        Assert.Equal(1, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Expired_unredeemed_slot_can_be_reissued_without_revoking_history()
    {
        await using var fixture = await Fixture.StartAsync();
        var old = await fixture.Store.CreateInvitationAsync("person@example.test",
            DateTimeOffset.UtcNow.AddDays(1));
        await fixture.ExecuteAsync("UPDATE invitations SET expires_at = created_at + 1 WHERE redeemable_email = 'person@example.test'");
        var request = await fixture.RequestAsync("person@example.test");
        Assert.Equal(OperatorMutationResult.Success,
            await fixture.Store.ApproveRequestAsync(request, "operator"));
        Assert.Equal(2, await fixture.CountAsync("invitations"));
        Assert.NotEqual(old, Assert.Single((await fixture.Store.ListInvitationRequestsAsync("approved"))!).InvitationId);
        Assert.Equal(0L, await fixture.ScalarAsync<long>(
            $"SELECT COUNT(*) FROM invitations WHERE id = '{old:N}' AND revoked_at IS NOT NULL"));
    }

    [Fact]
    public async Task Failed_request_update_rolls_back_invitation_creation()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = await fixture.RequestAsync("person@example.test");
        await fixture.ExecuteAsync("""
            CREATE TRIGGER fail_approval BEFORE UPDATE ON invitation_requests
            WHEN NEW.status = 'approved' BEGIN SELECT RAISE(ABORT, 'blocked'); END;
            """);
        Assert.Equal(OperatorMutationResult.Conflict,
            await fixture.Store.ApproveRequestAsync(id, "operator"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
        Assert.Single((await fixture.Store.ListInvitationRequestsAsync("pending"))!);
        await fixture.ExecuteAsync("DROP TRIGGER fail_approval");
        Assert.Equal(OperatorMutationResult.Success,
            await fixture.Store.ApproveRequestAsync(id, "operator"));
    }

    [Fact]
    public async Task Operator_mutations_fail_closed_on_missing_database_and_unbounded_identity()
    {
        await using var fixture = await Fixture.StartAsync(initialize: false);
        Assert.Equal(OperatorMutationResult.StorageUnavailable,
            await fixture.Store.ApproveRequestAsync(Guid.NewGuid(), "operator"));
        Assert.Equal(OperatorMutationResult.StorageUnavailable,
            await fixture.Store.RejectRequestAsync(Guid.NewGuid(), "operator"));
        Assert.Equal(OperatorMutationResult.StorageUnavailable,
            await fixture.Store.RevokeForOperatorAsync(Guid.NewGuid()));
        Assert.False(File.Exists(fixture.Path));
        await fixture.Store.InitializeAsync();
        var id = await fixture.RequestAsync("person@example.test");
        Assert.Equal(OperatorMutationResult.Conflict,
            await fixture.Store.ApproveRequestAsync(id, new string('x', 1100)));
        Assert.Single((await fixture.Store.ListInvitationRequestsAsync("pending"))!);
    }

    [Fact]
    public async Task Revocation_preserves_request_decision_and_binding_history()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = await fixture.RequestAsync("person@example.test");
        Assert.Equal(OperatorMutationResult.Success, await fixture.Store.ApproveRequestAsync(id, "operator"));
        var invitation = Assert.Single((await fixture.Store.ListInvitationRequestsAsync("approved"))!).InvitationId!.Value;
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("github", "123", "person@example.test")).Allowed);
        Assert.Equal(OperatorMutationResult.Success, await fixture.Store.RevokeForOperatorAsync(invitation));
        Assert.Equal(OperatorMutationResult.Conflict, await fixture.Store.RevokeForOperatorAsync(invitation));
        Assert.Equal(OperatorMutationResult.Missing, await fixture.Store.RevokeForOperatorAsync(Guid.NewGuid()));
        Assert.Equal(InvitationDecisionReason.Revoked,
            (await fixture.Store.CheckBoundIdentityAsync("github", "123")).Reason);
        Assert.Equal(1, await fixture.CountAsync("identity_bindings"));
        Assert.Equal(invitation, Assert.Single((await fixture.Store.ListInvitationRequestsAsync("approved"))!).InvitationId);
    }

    [Theory]
    [InlineData(null, null, null, null, null, false, HttpStatusCode.Unauthorized)]
    [InlineData("google", Issuer, Subject, ClientId, Subject, true, HttpStatusCode.Forbidden)]
    [InlineData("github", Issuer, Subject, ClientId, Subject, true, HttpStatusCode.Forbidden)]
    [InlineData("facebook", Issuer, Subject, ClientId, Subject, true, HttpStatusCode.Forbidden)]
    [InlineData("oidc", Issuer, "not-allowed", ClientId, "not-allowed", true, HttpStatusCode.Forbidden)]
    [InlineData("oidc", "https://other.example.test", Subject, ClientId, Subject, true, HttpStatusCode.Forbidden)]
    [InlineData("oidc", Issuer, Subject, "wrong-client", Subject, true, HttpStatusCode.Forbidden)]
    [InlineData("oidc", Issuer, Subject, ClientId, "different-principal", true, HttpStatusCode.Forbidden)]
    [InlineData("oidc", Issuer, Subject, ClientId, Subject, false, HttpStatusCode.Forbidden)]
    [InlineData("oidc", Issuer, Subject, ClientId, Subject, true, HttpStatusCode.OK)]
    public async Task Protected_cookie_operator_authorization_is_exact(
        string? provider, string? issuer, string? subject, string? client,
        string? principalSubject, bool metadata, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.StartAsync();
        var cookie = provider is null ? null : await fixture.SignInAsync(provider, issuer!, subject!, client!,
            principalSubject!, metadata);
        using var response = await fixture.GetAsync("/api/demo/operator/invitation-requests", cookie);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK) Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Missing_allowlist_denies_even_keycloak_and_admin_claims()
    {
        await using var fixture = await Fixture.StartAsync(allowOperator: false);
        var cookie = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject, role: "admin");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.GetAsync("/api/demo/operator/invitation-requests", cookie)).StatusCode);
    }

    [Fact]
    public async Task Malformed_allowlist_and_conflicting_ticket_claims_are_denied()
    {
        await using (var malformed = await Fixture.StartAsync(malformedAllowlist: true))
        {
            var cookie = await malformed.SignInAsync("oidc", Issuer, Subject, ClientId, Subject);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await malformed.GetAsync("/api/demo/operator/invitation-requests", cookie)).StatusCode);
        }
        await using var fixture = await Fixture.StartAsync();
        var contradictory = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject,
            rawSubject: "other");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.GetAsync("/api/demo/operator/invitation-requests", contradictory)).StatusCode);
        var wrongIssuer = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject,
            rawIssuer: "https://other.example.test");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.GetAsync("/api/demo/operator/invitation-requests", wrongIssuer)).StatusCode);
    }

    [Fact]
    public async Task Every_operator_route_requires_a_valid_cookie_and_allowlisted_operator()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = Guid.NewGuid().ToString("N");
        var paths = new[]
        {
            "/api/demo/operator/invitation-requests",
            $"/api/demo/operator/invitation-requests/{id}/approve",
            $"/api/demo/operator/invitation-requests/{id}/reject",
            $"/api/demo/operator/invitations/{id}/revoke"
        };
        var google = await fixture.SignInAsync("google", Issuer, Subject, ClientId, Subject);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.GetAsync(paths[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.GetAsync(paths[0], google)).StatusCode);
        foreach (var path in paths.Skip(1))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.PostAsync(path, "")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.PostAsync(path, google)).StatusCode);
        }
    }

    [Fact]
    public async Task Metadata_provider_mismatch_and_demo_subject_are_denied()
    {
        await using var fixture = await Fixture.StartAsync();
        var mismatch = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject,
            metadataProvider: "google");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.GetAsync("/api/demo/operator/invitation-requests", mismatch)).StatusCode);
        var demo = await fixture.SignInAsync("oidc", Issuer, "demo", ClientId, "demo", role: "admin");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.GetAsync("/api/demo/operator/invitation-requests", demo)).StatusCode);
    }

    [Fact]
    public async Task Listing_is_bounded_ordered_and_filterable()
    {
        await using var fixture = await Fixture.StartAsync();
        for (var i = 0; i < 52; i++)
            Assert.Equal(InvitationRequestResult.Accepted,
                await fixture.Store.CreateInvitationRequestAsync($"person{i:D2}@example.test"));
        var cookie = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject);
        using var pending = await fixture.GetAsync("/api/demo/operator/invitation-requests", cookie);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        Assert.Equal(50, json.RootElement.GetArrayLength());
        Assert.Equal("person00@example.test", json.RootElement[0].GetProperty("email").GetString());
        Assert.Equal("person49@example.test", json.RootElement[49].GetProperty("email").GetString());
        using var approved = await fixture.GetAsync("/api/demo/operator/invitation-requests?status=approved", cookie);
        Assert.Equal("[]", await approved.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.GetAsync("/api/demo/operator/invitation-requests?status=bogus", cookie)).StatusCode);
    }

    [Fact]
    public async Task Operator_reject_and_revoke_routes_preserve_history_and_invalidate_bound_cookie()
    {
        await using var fixture = await Fixture.StartAsync(enforceGithub: true);
        var operatorCookie = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject);
        var csrf = await fixture.CsrfAsync(operatorCookie);
        var rejectedId = await fixture.RequestAsync("rejected@example.test");
        var rejectPath = $"/api/demo/operator/invitation-requests/{rejectedId:N}/reject";
        Assert.Equal(HttpStatusCode.NoContent,
            (await fixture.PostAsync(rejectPath, operatorCookie, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await fixture.PostAsync(rejectPath, operatorCookie, csrf)).StatusCode);

        var approvedId = await fixture.RequestAsync("bound@example.test");
        Assert.Equal(HttpStatusCode.NoContent,
            (await fixture.PostAsync($"/api/demo/operator/invitation-requests/{approvedId:N}/approve",
                operatorCookie, csrf)).StatusCode);
        var invitation = Assert.Single((await fixture.Store.ListInvitationRequestsAsync("approved"))!).InvitationId!.Value;
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("github", "123", "bound@example.test")).Allowed);
        var githubCookie = await fixture.SignInAsync("github", Issuer, "123", ClientId, "123");
        Assert.Equal(HttpStatusCode.OK, (await fixture.GetAsync("/api/auth/me", githubCookie)).StatusCode);

        var revokePath = $"/api/demo/operator/invitations/{invitation:N}/revoke";
        Assert.Equal(HttpStatusCode.NoContent,
            (await fixture.PostAsync(revokePath, operatorCookie, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await fixture.PostAsync(revokePath, operatorCookie, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.GetAsync("/api/auth/me", githubCookie)).StatusCode);
        Assert.Equal(1, await fixture.CountAsync("identity_bindings"));
        Assert.Equal(1, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Mutations_require_operator_csrf_origin_and_no_body()
    {
        await using var fixture = await Fixture.StartAsync();
        var id = await fixture.RequestAsync("person@example.test");
        var cookie = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject);
        var path = $"/api/demo/operator/invitation-requests/{id:N}/approve";
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.PostAsync(path, cookie)).StatusCode);
        var csrf = await fixture.CsrfAsync(cookie);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync(path, cookie, ("invalid", csrf.Cookie))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.PostAsync(path, cookie, csrf, origin: "https://evil.example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync(path, cookie, csrf, body: "{}" )).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.PostAsync(path, cookie, csrf)).StatusCode);
        var auditId = $"{Issuer.Length}:{Issuer}{Subject.Length}:{Subject}";
        Assert.Equal(auditId, await fixture.ScalarAsync<string>(
            "SELECT decided_by FROM invitation_requests"));
        using (var listing = await fixture.GetAsync(
            "/api/demo/operator/invitation-requests?status=approved", cookie))
        {
            using var json = System.Text.Json.JsonDocument.Parse(await listing.Content.ReadAsStringAsync());
            Assert.Equal(System.Text.Json.JsonValueKind.String,
                json.RootElement[0].GetProperty("invitationId").ValueKind);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.PostAsync(path, cookie, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync("/api/demo/operator/invitation-requests/bad/approve", cookie, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.PostAsync($"/api/demo/operator/invitation-requests/{Guid.NewGuid():N}/approve", cookie, csrf)).StatusCode);
        Assert.Equal(1, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Disabled_workflow_has_no_operator_routes_and_missing_database_fails_generic()
    {
        await using (var disabled = await Fixture.StartAsync(enabled: false, initialize: false))
            Assert.Equal(HttpStatusCode.NotFound,
                (await disabled.GetAsync("/api/demo/operator/invitation-requests")).StatusCode);
        await using var fixture = await Fixture.StartAsync(initialize: false);
        var cookie = await fixture.SignInAsync("oidc", Issuer, Subject, ClientId, Subject);
        using var response = await fixture.GetAsync("/api/demo/operator/invitation-requests", cookie);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        var csrf = await fixture.CsrfAsync(cookie);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await fixture.PostAsync($"/api/demo/operator/invitation-requests/{Guid.NewGuid():N}/approve",
                cookie, csrf)).StatusCode);
        Assert.False(File.Exists(fixture.Path));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClient client;
        public string Path { get; }
        public SqliteInvitationStore Store { get; }
        private Fixture(WebApplication app, string path)
        {
            this.app = app;
            Path = path;
            Store = new SqliteInvitationStore(path);
            client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");
        }

        public static async Task<Fixture> StartAsync(bool enabled = true, bool initialize = true,
            bool allowOperator = true, bool enforceGithub = false, bool malformedAllowlist = false)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"operator-{Guid.NewGuid():N}.db");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var config = new Dictionary<string, string?>
            {
                ["Oidc:Authority"] = "https://oidc.example.test",
                ["Oidc:ClientId"] = ClientId,
                ["Oidc:ClientSecret"] = "secret"
            };
            if (allowOperator)
            {
                config["DemoInvitations:Workflow:Operators:0:Issuer"] = Issuer;
                if (!malformedAllowlist)
                    config["DemoInvitations:Workflow:Operators:0:Subject"] = Subject;
            }
            builder.Configuration.AddInMemoryCollection(config);
            builder.Services.AddOidcStarterBff(builder.Configuration);
            if (enforceGithub) builder.Services.AddInvitationSessionValidation(path, false, true);
            if (enabled)
            {
                builder.Services.AddInvitationRequestWorkflow(path);
                builder.Services.AddOperatorWorkflow();
            }
            var app = builder.Build();
            app.UseOidcStarterBff();
            if (enabled) app.UseRateLimiter();
            app.MapControllers();
            if (enabled)
            {
                app.MapInvitationRequestWorkflow();
                app.MapOperatorWorkflow();
            }
            app.MapGet("/test/signin", async (HttpContext context) =>
            {
                var q = context.Request.Query;
                var properties = new AuthenticationProperties();
                properties.Items["OidcStarter.AspNetCore.Bff.LoginProviderId"] = q["provider"];
                if (q["metadata"] != "false")
                {
                    properties.Items["OidcStarter.AspNetCore.Bff.ValidatedOidc.ProviderId"] = q["metadataProvider"].ToString() is { Length: > 0 } p ? p : q["provider"];
                    properties.Items["OidcStarter.AspNetCore.Bff.ValidatedOidc.AuthenticationScheme"] = "OpenIdConnect";
                    properties.Items["OidcStarter.AspNetCore.Bff.ValidatedOidc.Issuer"] = q["issuer"];
                    properties.Items["OidcStarter.AspNetCore.Bff.ValidatedOidc.Subject"] = q["subject"];
                    properties.Items["OidcStarter.AspNetCore.Bff.ValidatedOidc.ClientId"] = q["client"];
                }
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, q["principalSubject"]!) };
                if (q["role"].ToString() is { Length: > 0 } role) claims.Add(new Claim(ClaimTypes.Role, role));
                if (q["rawSubject"].ToString() is { Length: > 0 } rawSubject)
                    claims.Add(new Claim("sub", rawSubject));
                if (q["rawIssuer"].ToString() is { Length: > 0 } rawIssuer)
                    claims.Add(new Claim("iss", rawIssuer));
                if (q["provider"] == "github") InvitationSessionValidation.Mark(properties, "github", q["principalSubject"]!);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), properties);
            });
            await app.StartAsync();
            var fixture = new Fixture(app, path);
            if (initialize) await fixture.Store.InitializeAsync();
            return fixture;
        }

        public async Task<Guid> RequestAsync(string email)
        {
            Assert.Equal(InvitationRequestResult.Accepted, await Store.CreateInvitationRequestAsync(email));
            return Assert.Single((await Store.ListInvitationRequestsAsync("pending"))!).Id;
        }

        public async Task<string> SignInAsync(string provider, string issuer, string subject, string clientId,
            string principalSubject, bool metadata = true, string? role = null, string? metadataProvider = null,
            string? rawSubject = null, string? rawIssuer = null)
        {
            var uri = "/test/signin?" + string.Join('&', new Dictionary<string, string>
            {
                ["provider"] = provider, ["issuer"] = issuer, ["subject"] = subject,
                ["client"] = clientId, ["principalSubject"] = principalSubject,
                ["metadata"] = metadata.ToString().ToLowerInvariant(), ["role"] = role ?? "",
                ["metadataProvider"] = metadataProvider ?? "", ["rawSubject"] = rawSubject ?? "",
                ["rawIssuer"] = rawIssuer ?? ""
            }.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
            using var response = await client.GetAsync(uri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return Assert.Single(response.Headers.GetValues("Set-Cookie"),
                value => value.StartsWith("__Host-oidc-starter-bff=", StringComparison.Ordinal)).Split(';')[0];
        }

        public Task<HttpResponseMessage> GetAsync(string path, string? cookie = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (cookie is not null) request.Headers.TryAddWithoutValidation("Cookie", cookie);
            return client.SendAsync(request);
        }

        public async Task<(string Token, string Cookie)> CsrfAsync(string cookie)
        {
            using var response = await GetAsync("/api/auth/csrf", cookie);
            var headers = response.Headers.GetValues("Set-Cookie").ToArray();
            var token = Assert.Single(headers, value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)).Split(';')[0];
            var antiforgeryCookie = Assert.Single(headers, value => value.StartsWith("__Host-oidc-starter-bff-af=", StringComparison.Ordinal)).Split(';')[0];
            return (Uri.UnescapeDataString(token["XSRF-TOKEN=".Length..]), antiforgeryCookie);
        }

        public Task<HttpResponseMessage> PostAsync(string path, string cookie,
            (string Token, string Cookie)? csrf = null, string origin = "https://localhost", string? body = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.TryAddWithoutValidation("Origin", origin);
            request.Headers.TryAddWithoutValidation("Cookie", csrf is { } token ? $"{cookie}; {token.Cookie}" : cookie);
            if (csrf is { } value) request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", value.Token);
            if (body is not null) request.Content = new StringContent(body);
            return client.SendAsync(request);
        }

        public async Task<int> CountAsync(string table)
        {
            await using var connection = new SqliteConnection($"Data Source={Path};Mode=ReadWrite;Pooling=False");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table}";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new SqliteConnection($"Data Source={Path};Mode=ReadWrite;Pooling=False");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqliteConnection($"Data Source={Path};Mode=ReadWrite;Pooling=False");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
            File.Delete(Path);
        }
    }
}
