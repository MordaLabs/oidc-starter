using System.Diagnostics;
using Backend.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Backend.Tests;

public sealed class InvitationDatabaseProvisioningTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative.db")]
    public async Task Missing_or_relative_path_is_rejected(string? path)
    {
        using var fixture = new Fixture();
        var result = await RunAsync(path);
        Assert.Equal(2, result.Code);
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath));
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task Missing_parent_and_directory_as_database_are_rejected()
    {
        using var fixture = new Fixture();
        Assert.Equal(2, (await RunAsync(Path.Combine(fixture.DirectoryPath, "missing", "db.sqlite"))).Code);
        Assert.Equal(2, (await RunAsync(fixture.DirectoryPath)).Code);
        Assert.Empty(Directory.GetFiles(fixture.DirectoryPath));
    }

    [Fact]
    public async Task Exact_mode_is_required_and_extra_arguments_are_rejected()
    {
        using var fixture = new Fixture();
        Assert.False(InvitationDatabaseProvisioning.IsRequested([]));
        Assert.False(InvitationDatabaseProvisioning.IsRequested(["--DemoInvitations:Workflow:Enabled=true"]));
        Assert.False(InvitationDatabaseProvisioning.IsRequested(["--DemoInvitations:DatabasePath=ignored.db"]));
        Assert.True(InvitationDatabaseProvisioning.IsRequested(["--provision-demo-invitations=true"]));
        Assert.Equal(2, (await RunAsync(fixture.DatabasePath,
            [InvitationDatabaseProvisioning.Command, "--urls=http://127.0.0.1:12345"])).Code);
        Assert.False(File.Exists(fixture.DatabasePath));
    }

    [Fact]
    public async Task First_provisioning_creates_usable_schema_and_preserves_unique_constraints()
    {
        using var fixture = new Fixture();
        var result = await RunAsync(fixture.DatabasePath);
        Assert.Equal(0, result.Code);
        Assert.True(File.Exists(fixture.DatabasePath));
        Assert.Equal(InvitationDecisionReason.BindingMissing,
            (await new SqliteInvitationStore(fixture.DatabasePath).CheckBoundIdentityAsync("google", "new")).Reason);

        var store = new SqliteInvitationStore(fixture.DatabasePath);
        Assert.Equal(InvitationRequestResult.Accepted,
            await store.CreateInvitationRequestAsync("pending@example.test"));
        await using var connection = await fixture.OpenAsync();
        using var duplicatePending = connection.CreateCommand();
        duplicatePending.CommandText = """
            INSERT INTO invitation_requests (id, canonical_email, created_at, status)
            VALUES ('duplicate', 'pending@example.test', 1, 'pending');
            """;
        await Assert.ThrowsAsync<SqliteException>(() => duplicatePending.ExecuteNonQueryAsync());

        var first = await store.CreateInvitationAsync("first@example.test");
        var second = await store.CreateInvitationAsync("second@example.test");
        Assert.True(await store.ApproveInvitationAsync(first));
        Assert.True(await store.ApproveInvitationAsync(second));
        Assert.True((await store.BindVerifiedIdentityAsync("google", "stable", "first@example.test")).Allowed);
        using var duplicateIdentity = connection.CreateCommand();
        duplicateIdentity.CommandText = """
            INSERT INTO identity_bindings (invitation_id, provider_id, provider_subject, bound_at)
            VALUES ($id, 'google', 'stable', 1);
            """;
        duplicateIdentity.Parameters.AddWithValue("$id", second.ToString("N"));
        await Assert.ThrowsAsync<SqliteException>(() => duplicateIdentity.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Repeated_provisioning_preserves_invitations_bindings_and_request_decisions()
    {
        using var fixture = new Fixture();
        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
        var store = new SqliteInvitationStore(fixture.DatabasePath);
        var approved = await store.CreateInvitationAsync("bound@example.test");
        Assert.True(await store.ApproveInvitationAsync(approved));
        Assert.True((await store.BindVerifiedIdentityAsync("github", "123", "bound@example.test")).Allowed);

        await store.CreateInvitationRequestAsync("approve@example.test");
        await store.CreateInvitationRequestAsync("reject@example.test");
        var pending = await store.ListInvitationRequestsAsync("pending");
        Assert.NotNull(pending);
        var toApprove = Assert.Single(pending, record => record.Email == "approve@example.test");
        var toReject = Assert.Single(pending, record => record.Email == "reject@example.test");
        Assert.Equal(OperatorMutationResult.Success,
            await store.ApproveRequestAsync(toApprove.Id, "operator"));
        Assert.Equal(OperatorMutationResult.Success,
            await store.RejectRequestAsync(toReject.Id, "operator"));

        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
        Assert.Equal(approved, (await store.CheckBoundIdentityAsync("github", "123")).InvitationId);
        Assert.Single((await store.ListInvitationRequestsAsync("approved"))!, record => record.Id == toApprove.Id);
        Assert.Single((await store.ListInvitationRequestsAsync("rejected"))!, record => record.Id == toReject.Id);
        Assert.Equal(OperatorMutationResult.Conflict,
            await store.ApproveRequestAsync(toApprove.Id, "operator"));
        Assert.True((await store.BindVerifiedIdentityAsync("google", "approved-subject", "approve@example.test")).Allowed);
    }

    [Fact]
    public async Task Incompatible_existing_schema_is_rejected_without_partial_schema_changes()
    {
        using var fixture = new Fixture();
        await using (var connection = await fixture.OpenAsync(create: true))
        {
            using var create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE invitations (id TEXT PRIMARY KEY);";
            await create.ExecuteNonQueryAsync();
        }

        var result = await RunAsync(fixture.DatabasePath);
        Assert.Equal(1, result.Code);
        Assert.Empty(result.Output);
        await using var reopened = await fixture.OpenAsync();
        using var inspect = reopened.CreateCommand();
        inspect.CommandText = "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        Assert.Equal("invitations", await inspect.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Wrong_pending_index_is_rejected_even_when_its_name_exists()
    {
        using var fixture = new Fixture();
        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
        await using (var connection = await fixture.OpenAsync())
        {
            using var change = connection.CreateCommand();
            change.CommandText = """
                DROP INDEX one_pending_invitation_request;
                CREATE UNIQUE INDEX one_pending_invitation_request
                    ON invitation_requests(canonical_email) WHERE status = 'approved';
                """;
            await change.ExecuteNonQueryAsync();
        }
        Assert.Equal(1, (await RunAsync(fixture.DatabasePath)).Code);
    }

    [Fact]
    public async Task Extra_trigger_is_rejected_without_changing_existing_data_or_schema()
    {
        using var fixture = new Fixture();
        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
        var store = new SqliteInvitationStore(fixture.DatabasePath);
        var invitationId = await store.CreateInvitationAsync("preserved@example.test");
        Assert.True(await store.ApproveInvitationAsync(invitationId));
        await using (var connection = await fixture.OpenAsync())
        {
            using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TRIGGER unexpected_revoke AFTER INSERT ON invitations
                BEGIN
                    UPDATE invitations SET revoked_at = 123 WHERE id = NEW.id;
                END;
                """;
            await create.ExecuteNonQueryAsync();
        }

        Assert.Equal(1, (await RunAsync(fixture.DatabasePath)).Code);
        await using var reopened = await fixture.OpenAsync();
        using var inspect = reopened.CreateCommand();
        inspect.CommandText = """
            SELECT approved_at IS NOT NULL AND revoked_at IS NULL FROM invitations WHERE id = $id;
            """;
        inspect.Parameters.AddWithValue("$id", invitationId.ToString("N"));
        Assert.Equal(1L, await inspect.ExecuteScalarAsync());
        using var trigger = reopened.CreateCommand();
        trigger.CommandText = "SELECT count(*) FROM sqlite_schema WHERE type = 'trigger' AND name = 'unexpected_revoke';";
        Assert.Equal(1L, await trigger.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Extra_unique_index_is_rejected_without_removing_it()
    {
        using var fixture = new Fixture();
        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
        await using (var connection = await fixture.OpenAsync())
        {
            using var create = connection.CreateCommand();
            create.CommandText = "CREATE UNIQUE INDEX unexpected_unique ON invitations(canonical_email);";
            await create.ExecuteNonQueryAsync();
        }

        Assert.Equal(1, (await RunAsync(fixture.DatabasePath)).Code);
        await using var reopened = await fixture.OpenAsync();
        using var inspect = reopened.CreateCommand();
        inspect.CommandText = "SELECT count(*) FROM sqlite_schema WHERE type = 'index' AND name = 'unexpected_unique';";
        Assert.Equal(1L, await inspect.ExecuteScalarAsync());
    }

    [Fact]
    public async Task SQLite_autoindexes_are_accepted_on_repeated_provisioning()
    {
        using var fixture = new Fixture();
        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
        await using (var connection = await fixture.OpenAsync())
        {
            using var inspect = connection.CreateCommand();
            inspect.CommandText = """
                SELECT count(*) FROM sqlite_schema
                WHERE name GLOB 'sqlite_autoindex_*' AND sql IS NULL;
                """;
            Assert.True((long)(await inspect.ExecuteScalarAsync())! > 0);
        }
        Assert.Equal(0, (await RunAsync(fixture.DatabasePath)).Code);
    }

    [Fact]
    public async Task Invalid_database_file_fails_without_replacement()
    {
        using var fixture = new Fixture();
        await File.WriteAllTextAsync(fixture.DatabasePath, "not a SQLite database");
        Assert.Equal(1, (await RunAsync(fixture.DatabasePath)).Code);
        Assert.Equal("not a SQLite database", await File.ReadAllTextAsync(fixture.DatabasePath));
    }

    [Fact]
    public async Task One_shot_backend_process_exits_without_starting_http()
    {
        using var fixture = new Fixture();
        var result = await RunBackendProcessAsync(fixture, InvitationDatabaseProvisioning.Command);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("schema provisioning completed", result.Output);
        Assert.DoesNotContain("Now listening", result.Output + result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(fixture.DatabasePath));
    }

    [Fact]
    public async Task Malformed_provisioning_switch_exits_two_without_starting_http_or_writing_database()
    {
        using var fixture = new Fixture();
        var result = await RunBackendProcessAsync(fixture, "--provision-demo-invitations=true");
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("requires only", result.Error);
        Assert.DoesNotContain("Now listening", result.Output + result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(fixture.DatabasePath));
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunBackendProcessAsync(
        Fixture fixture, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = fixture.DirectoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        info.ArgumentList.Add(typeof(SqliteInvitationStore).Assembly.Location);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["DemoInvitations__DatabasePath"] = fixture.DatabasePath;
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["DOTNET_ENVIRONMENT"] = "Production";
        using var process = Process.Start(info);
        Assert.NotNull(process);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        return (process.ExitCode, output, error);
    }

    private static async Task<(int Code, string Output, string Error)> RunAsync(
        string? path, string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DemoInvitations:DatabasePath"] = path }).Build();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await InvitationDatabaseProvisioning.RunAsync(
            args ?? [InvitationDatabaseProvisioning.Command], configuration, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"invitation-provisioning-{Guid.NewGuid():N}");
        public string DatabasePath => Path.Combine(DirectoryPath, "invitations.db");

        public Fixture() => Directory.CreateDirectory(DirectoryPath);

        public async Task<SqliteConnection> OpenAsync(bool create = false)
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString());
            await connection.OpenAsync();
            return connection;
        }

        public void Dispose()
        {
            var target = Path.GetFullPath(DirectoryPath);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!target.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(target).StartsWith("invitation-provisioning-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test directory.");
            Directory.Delete(target, recursive: true);
        }
    }
}
