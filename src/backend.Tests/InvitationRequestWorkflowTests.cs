using System.Net;
using Backend.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace Backend.Tests;

public sealed class InvitationRequestWorkflowTests
{
    [Fact]
    public async Task Provisioning_adds_requests_without_changing_existing_bindings()
    {
        await using var fixture = await Fixture.StartAsync(true);
        var invitation = await fixture.Store.CreateInvitationAsync("bound@example.test");
        Assert.True(await fixture.Store.ApproveInvitationAsync(invitation));
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("google", "subject", "bound@example.test")).Allowed);
        await using (var connection = new SqliteConnection($"Data Source={fixture.Path};Mode=ReadWrite;Pooling=False"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE invitation_requests";
            await command.ExecuteNonQueryAsync();
        }
        await fixture.Store.InitializeAsync();

        Assert.Equal(InvitationRequestResult.Accepted,
            await fixture.Store.CreateInvitationRequestAsync("  PERSON@Example.TEST  "));
        Assert.Equal(InvitationRequestResult.Accepted,
            await fixture.Store.CreateInvitationRequestAsync("person@example.test"));
        Assert.Equal(1, await fixture.CountAsync("invitation_requests"));
        Assert.Equal("person@example.test", await fixture.SingleRequestEmailAsync());
        Assert.Equal(1, await fixture.CountAsync("invitations"));
        Assert.Equal(InvitationDecisionReason.BoundIdentity,
            (await fixture.Store.CheckBoundIdentityAsync("google", "subject")).Reason);
    }

    [Fact]
    public async Task Concurrent_duplicates_are_one_pending_request_and_no_invitation()
    {
        await using var fixture = await Fixture.StartAsync(true);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            fixture.Store.CreateInvitationRequestAsync("RACE@example.test")));
        Assert.All(results, result => Assert.Equal(InvitationRequestResult.Accepted, result));
        Assert.Equal(1, await fixture.CountAsync("invitation_requests"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
        Assert.Equal(0, await fixture.CountAsync("identity_bindings"));
        Assert.Equal(InvitationDecisionReason.BindingMissing,
            (await fixture.Store.CheckBoundIdentityAsync("github", "123")).Reason);
        Assert.Equal(InvitationDecisionReason.InvitationMissing,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "subject", "race@example.test")).Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("name..part@example.test")]
    public async Task Invalid_email_is_not_saved(string email)
    {
        await using var fixture = await Fixture.StartAsync(true);
        Assert.Equal(InvitationRequestResult.InvalidEmail,
            await fixture.Store.CreateInvitationRequestAsync(email));
        Assert.Equal(0, await fixture.CountAsync("invitation_requests"));
    }

    [Fact]
    public async Task Missing_database_and_missing_schema_fail_without_implicit_creation()
    {
        await using var fixture = await Fixture.StartAsync(true, initialize: false);
        Assert.Equal(InvitationRequestResult.StorageUnavailable,
            await fixture.Store.CreateInvitationRequestAsync("person@example.test"));
        Assert.False(File.Exists(fixture.Path));
        await using (var connection = new SqliteConnection($"Data Source={fixture.Path};Pooling=False"))
            await connection.OpenAsync();
        Assert.Equal(InvitationRequestResult.StorageUnavailable,
            await fixture.Store.CreateInvitationRequestAsync("person@example.test"));
    }

    [Fact]
    public async Task Disabled_route_is_absent_and_does_not_need_database()
    {
        await using var fixture = await Fixture.StartAsync(false, initialize: false);
        using var response = await fixture.PostAsync("""{"email":"person@example.test"}""");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(File.Exists(fixture.Path));
    }

    [Fact]
    public async Task Anonymous_valid_request_and_duplicate_have_identical_neutral_response()
    {
        await using var fixture = await Fixture.StartAsync(true);
        var csrf = await fixture.GetCsrfAsync();
        using var first = await fixture.PostAsync("""{"email":"Person@Example.test"}""", csrf: csrf);
        using var second = await fixture.PostAsync("""{"email":"person@example.TEST"}""", csrf: csrf);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Empty(await first.Content.ReadAsStringAsync());
        Assert.False(first.Headers.Contains("Location"));
        Assert.False(first.Headers.TryGetValues("Set-Cookie", out var responseCookies)
            && responseCookies.Any(value => value.StartsWith("__Host-oidc-starter-bff=", StringComparison.Ordinal)));
        Assert.Equal(1, await fixture.CountAsync("invitation_requests"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
        Assert.Equal(0, await fixture.CountAsync("identity_bindings"));
        Assert.Equal(InvitationDecisionReason.InvitationMissing,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "subject", "person@example.test")).Reason);
        Assert.Equal(InvitationDecisionReason.InvitationMissing,
            (await fixture.Store.BindVerifiedIdentityAsync("github", "123", "person@example.test")).Reason);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await fixture.GetAsync("/api/demo/invitation-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.GetAsync("/api/demo/invitation-requests/status")).StatusCode);
    }

    [Fact]
    public void Enabled_workflow_requires_absolute_path_and_shares_gate_store()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddInvitationRequestWorkflow("relative.db"));
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"request-{Guid.NewGuid():N}.db");
        services.AddGoogleInvitationGate(path);
        services.AddGitHubInvitationGate(path);
        services.AddInvitationRequestWorkflow(path);
        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<SqliteInvitationStore>());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Csrf_origin_json_and_body_limit_fail_closed()
    {
        await using var fixture = await Fixture.StartAsync(true);
        var csrf = await fixture.GetCsrfAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync("""{"email":"person@example.test"}""")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await fixture.PostAsync("""{"email":"person@example.test"}""", csrf: csrf,
                origin: "https://evil.example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync("{", csrf: csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync("""{"email":"person@example.test","status":"approved"}""", csrf: csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync("""{"email":"a@example.test","email":"b@example.test"}""", csrf: csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await fixture.PostAsync("""{"email":"person@example.test"}""" + new string(' ', 1100),
                csrf: csrf)).StatusCode);
        Assert.Equal(0, await fixture.CountAsync("invitation_requests"));
    }

    [Fact]
    public async Task Oversized_json_content_length_is_rejected_before_any_body_read()
    {
        await using var fixture = await Fixture.StartAsync(true);
        var csrf = await fixture.GetCsrfAsync();
        using var response = await fixture.PostAsync(new string('x', 1100), csrf: csrf);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(fixture.LastContentLength > 1024);
        Assert.Equal(0, fixture.LastBodyReadCalls);
        Assert.Equal(0, await fixture.CountAsync("invitation_requests"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
    }

    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("text/plain")]
    public async Task Non_json_body_is_rejected_without_form_or_other_body_parsing(string contentType)
    {
        await using var fixture = await Fixture.StartAsync(true);
        var csrf = await fixture.GetCsrfAsync();
        using var content = new StringContent(new string('x', 1100));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        using var response = await fixture.PostContentAsync(content, csrf, includeCsrfHeader: false);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.LastBodyReadCalls);
        Assert.Equal(0, await fixture.CountAsync("invitation_requests"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Unknown_length_json_is_read_only_to_the_1025_byte_boundary()
    {
        await using var fixture = await Fixture.StartAsync(true);
        var csrf = await fixture.GetCsrfAsync();
        using var content = new StreamContent(new NonSeekableStream(
            System.Text.Encoding.UTF8.GetBytes("""{"email":"person@example.test"}""" + new string(' ', 4000))));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        using var response = await fixture.PostContentAsync(content, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fixture.LastContentLength);
        Assert.Equal(1025, fixture.LastBodyReadBytes);
        Assert.Equal(0, await fixture.CountAsync("invitation_requests"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Valid_sized_json_still_requires_csrf_and_accepts_valid_submission()
    {
        await using var fixture = await Fixture.StartAsync(true);
        using var missing = await fixture.PostAsync("""{"email":"person@example.test"}""");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var csrf = await fixture.GetCsrfAsync();
        using var invalid = await fixture.PostAsync("""{"email":"person@example.test"}""",
            ("invalid", csrf.Cookie));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var accepted = await fixture.PostAsync("""{"email":"person@example.test"}""", csrf);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(1, await fixture.CountAsync("invitation_requests"));
        Assert.Equal(0, await fixture.CountAsync("invitations"));
    }

    [Fact]
    public async Task Excessive_requests_are_rate_limited_and_missing_database_is_generic()
    {
        await using var fixture = await Fixture.StartAsync(true, initialize: false);
        var csrf = await fixture.GetCsrfAsync();
        for (var i = 0; i < 10; i++)
        {
            using var response = await fixture.PostAsync("""{"email":"person@example.test"}""", csrf: csrf);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
        using var limited = await fixture.PostAsync("""{"email":"person@example.test"}""", csrf: csrf);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.False(File.Exists(fixture.Path));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private sealed class BodyReads
        {
            public int Calls;
            public long Bytes;
            public long? ContentLength;
        }

        private readonly WebApplication app;
        private readonly HttpClient client;
        private readonly BodyReads reads;
        public string Path { get; }
        public SqliteInvitationStore Store { get; }
        public int LastBodyReadCalls => reads.Calls;
        public long LastBodyReadBytes => reads.Bytes;
        public long? LastContentLength => reads.ContentLength;

        private Fixture(WebApplication app, string path, BodyReads reads)
        {
            this.app = app;
            this.reads = reads;
            Path = path;
            Store = new SqliteInvitationStore(path);
            client = app.GetTestClient();
            client.BaseAddress = new Uri("https://localhost");
        }

        public static async Task<Fixture> StartAsync(bool enabled, bool initialize = true)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"request-{Guid.NewGuid():N}.db");
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:Authority"] = "https://oidc.example.test",
                ["Oidc:ClientId"] = "oidc-client",
                ["Oidc:ClientSecret"] = "oidc-secret"
            });
            builder.Services.AddOidcStarterBff(builder.Configuration);
            if (enabled) builder.Services.AddInvitationRequestWorkflow(path);
            var app = builder.Build();
            var reads = new BodyReads();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/api/demo/invitation-requests")
                {
                    var monitored = new CountingStream(context.Request.Body);
                    context.Request.Body = monitored;
                    reads.ContentLength = context.Request.ContentLength;
                    try { await next(); }
                    finally
                    {
                        reads.Calls = monitored.Calls;
                        reads.Bytes = monitored.Bytes;
                    }
                }
                else await next();
            });
            app.UseOidcStarterBff();
            if (enabled) app.UseRateLimiter();
            app.MapControllers();
            if (enabled) app.MapInvitationRequestWorkflow();
            await app.StartAsync();
            var fixture = new Fixture(app, path, reads);
            if (initialize) await fixture.Store.InitializeAsync();
            return fixture;
        }

        public async Task<(string Token, string Cookie)> GetCsrfAsync()
        {
            using var response = await client.GetAsync("/api/auth/csrf");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
            var token = Assert.Single(cookies, value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)).Split(';')[0];
            var cookie = Assert.Single(cookies, value => value.StartsWith("__Host-oidc-starter-bff-af=", StringComparison.Ordinal)).Split(';')[0];
            return (Uri.UnescapeDataString(token["XSRF-TOKEN=".Length..]), cookie);
        }

        public Task<HttpResponseMessage> GetAsync(string path) => client.GetAsync(path);

        public Task<HttpResponseMessage> PostAsync(string body, (string Token, string Cookie)? csrf = null,
            string origin = "https://localhost")
        {
            return PostContentAsync(new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                csrf, origin);
        }

        public Task<HttpResponseMessage> PostContentAsync(HttpContent content,
            (string Token, string Cookie)? csrf = null, string origin = "https://localhost",
            bool includeCsrfHeader = true)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/demo/invitation-requests") { Content = content };
            request.Headers.TryAddWithoutValidation("Origin", origin);
            if (csrf is { } token)
            {
                request.Headers.TryAddWithoutValidation("Cookie", token.Cookie);
                if (includeCsrfHeader) request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token.Token);
            }
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

        public async Task<string> SingleRequestEmailAsync()
        {
            await using var connection = new SqliteConnection($"Data Source={Path};Mode=ReadWrite;Pooling=False");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT canonical_email FROM invitation_requests";
            return (string)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
            File.Delete(Path);
        }
    }

    private sealed class CountingStream(Stream inner) : Stream
    {
        public int Calls { get; private set; }
        public long Bytes { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override int Read(byte[] buffer, int offset, int count)
        {
            Calls++;
            var read = inner.Read(buffer, offset, count);
            Bytes += read;
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Calls++;
            var read = await inner.ReadAsync(buffer, cancellationToken);
            Bytes += read;
            return read;
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            Calls++;
            var read = await inner.ReadAsync(buffer, offset, count, cancellationToken);
            Bytes += read;
            return read;
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
