using Backend.Auth;
using Microsoft.Data.Sqlite;

namespace Backend.Tests;

public sealed class SqliteInvitationStoreTests
{
    [Fact]
    public async Task Approval_and_canonical_email_are_required_for_first_binding()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Store.CreateInvitationAsync("  Alice+Demo@Example.TEST  ");

        Assert.Equal(InvitationDecisionReason.ApprovalRequired,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "google-1", "alice+demo@example.test")).Reason);
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        Assert.False(await fixture.Store.ApproveInvitationAsync(id));

        var result = await fixture.Store.BindVerifiedIdentityAsync("GOOGLE", "google-1", "ALICE+DEMO@example.test");
        Assert.True(result.Allowed);
        Assert.Equal(InvitationDecisionReason.NewBinding, result.Reason);
        Assert.Equal(id, result.InvitationId);
        Assert.False((await fixture.Store.BindVerifiedIdentityAsync("google", "google-2", "alice.demo@example.test")).Allowed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("name@example.test, other@example.test")]
    [InlineData("Name <name@example.test>")]
    [InlineData("name..part@example.test")]
    [InlineData("name@-example.test")]
    [InlineData("name@example.test@other.test")]
    [InlineData("name@\nexample.test")]
    public async Task Invalid_or_ambiguous_email_is_rejected(string email)
    {
        using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store.CreateInvitationAsync(email));
    }

    [Fact]
    public async Task No_two_redeemable_invitations_can_use_the_same_canonical_email()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.Store.CreateInvitationAsync("A+tag@Example.test");
        await Assert.ThrowsAsync<SqliteException>(() => fixture.Store.CreateInvitationAsync("a+TAG@example.TEST"));
        await fixture.Store.CreateInvitationAsync("a.tag@example.test");
    }

    [Fact]
    public async Task Expiration_blocks_redemption_at_boundary_but_not_a_prior_binding()
    {
        using var fixture = await Fixture.CreateAsync();
        var deadline = fixture.Clock.GetUtcNow().AddMinutes(5);
        var id = await fixture.Store.CreateInvitationAsync("expires@example.test", deadline);
        Assert.True(await fixture.Store.ApproveInvitationAsync(id));
        fixture.Clock.Set(deadline);
        Assert.Equal(InvitationDecisionReason.Expired,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "subject", "expires@example.test")).Reason);

        // An expired, unbound invitation can be replaced explicitly by the operator.
        var replacement = await fixture.Store.CreateInvitationAsync("EXPIRES@example.test");
        Assert.NotEqual(id, replacement);
        Assert.True(await fixture.Store.ApproveInvitationAsync(replacement));
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("google", "subject", "expires@example.test")).Allowed);
        fixture.Clock.Set(deadline.AddDays(1));
        Assert.Equal(InvitationDecisionReason.BoundIdentity,
            (await fixture.Store.CheckBoundIdentityAsync("google", "subject")).Reason);
    }

    [Fact]
    public async Task Revocation_blocks_both_redemption_and_existing_identity()
    {
        using var fixture = await Fixture.CreateAsync();
        var pending = await fixture.Store.CreateInvitationAsync("pending@example.test");
        Assert.True(await fixture.Store.RevokeInvitationAsync(pending));
        Assert.Equal(InvitationDecisionReason.Revoked,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "first", "pending@example.test")).Reason);

        var bound = await fixture.ApproveAsync("bound@example.test");
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("github", "123", "bound@example.test")).Allowed);
        Assert.True(await fixture.Store.RevokeInvitationAsync(bound));
        Assert.False(await fixture.Store.RevokeInvitationAsync(bound));
        Assert.Equal(InvitationDecisionReason.Revoked,
            (await fixture.Store.CheckBoundIdentityAsync("github", "123")).Reason);
    }

    [Fact]
    public async Task Bound_subject_survives_email_change_and_other_identities_cannot_reuse_invitation()
    {
        using var fixture = await Fixture.CreateAsync();
        var id = await fixture.ApproveAsync("person@example.test");
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("google", "google-123", "person@example.test")).Allowed);

        var returning = await fixture.Store.BindVerifiedIdentityAsync("google", "google-123", "changed@example.test");
        Assert.True(returning.Allowed);
        Assert.Equal(InvitationDecisionReason.BoundIdentity, returning.Reason);
        Assert.Equal(id, returning.InvitationId);
        Assert.Equal(InvitationDecisionReason.AlreadyBound,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "google-456", "person@example.test")).Reason);
        Assert.Equal(InvitationDecisionReason.AlreadyBound,
            (await fixture.Store.BindVerifiedIdentityAsync("github", "456", "person@example.test")).Reason);
    }

    [Fact]
    public async Task Provider_scoped_subjects_are_distinct_and_invalid_providers_are_denied()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.ApproveAsync("one@example.test");
        await fixture.ApproveAsync("two@example.test");

        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("google", "123", "one@example.test")).Allowed);
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("github", "00123", "two@example.test")).Allowed);
        Assert.True((await fixture.Store.CheckBoundIdentityAsync("github", "123")).Allowed);
        Assert.Equal(InvitationDecisionReason.UnsupportedProvider,
            (await fixture.Store.BindVerifiedIdentityAsync("facebook", "123", "one@example.test")).Reason);
        Assert.Equal(InvitationDecisionReason.InvalidSubject,
            (await fixture.Store.CheckBoundIdentityAsync("github", "not-a-number")).Reason);
    }

    [Fact]
    public async Task Concurrent_first_bindings_have_exactly_one_winner()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.ApproveAsync("race@example.test");
        using var start = new ManualResetEventSlim();
        var attempts = Enumerable.Range(1, 8)
            .Select(index => Task.Run(async () =>
            {
                start.Wait();
                return await fixture.Store.BindVerifiedIdentityAsync("github", index.ToString(), "race@example.test");
            }))
            .ToArray();
        start.Set();

        var results = await Task.WhenAll(attempts);
        Assert.Single(results, static result => result.Allowed);
        Assert.All(results.Where(static result => !result.Allowed),
            static result => Assert.Equal(InvitationDecisionReason.AlreadyBound, result.Reason));
    }

    [Fact]
    public async Task Binding_is_durable_across_store_instances_and_reopen()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.ApproveAsync("durable@example.test");
        Assert.True((await fixture.Store.BindVerifiedIdentityAsync("google", "stable", "durable@example.test")).Allowed);

        var reopened = new SqliteInvitationStore(fixture.Path, fixture.Clock);
        Assert.Equal(InvitationDecisionReason.BoundIdentity,
            (await reopened.CheckBoundIdentityAsync("google", "stable")).Reason);
        await reopened.InitializeAsync();
        Assert.Equal(InvitationDecisionReason.BoundIdentity,
            (await reopened.CheckBoundIdentityAsync("google", "stable")).Reason);
    }

    [Fact]
    public async Task Missing_schema_or_unavailable_storage_never_allows_access()
    {
        using var fixture = await Fixture.CreateAsync();
        var missingFile = Path.Combine(fixture.DirectoryPath, "uninitialized.db");
        var absent = new SqliteInvitationStore(missingFile, fixture.Clock);
        Assert.False(File.Exists(missingFile));
        Assert.Equal(InvitationDecisionReason.StorageUnavailable,
            (await absent.CheckBoundIdentityAsync("google", "subject")).Reason);
        Assert.False(File.Exists(missingFile));
        Assert.Equal(InvitationDecisionReason.StorageUnavailable,
            (await absent.BindVerifiedIdentityAsync("google", "subject", "name@example.test")).Reason);
        Assert.False(File.Exists(missingFile));

        var badPath = new SqliteInvitationStore(Path.Combine(fixture.DirectoryPath, "missing", "store.db"), fixture.Clock);
        Assert.False((await badPath.CheckBoundIdentityAsync("google", "subject")).Allowed);
    }

    [Fact]
    public async Task Only_explicit_initialization_creates_a_new_database_and_schema()
    {
        using var fixture = await Fixture.CreateAsync();
        var path = Path.Combine(fixture.DirectoryPath, "provisioned.db");
        var store = new SqliteInvitationStore(path, fixture.Clock);
        Assert.False(File.Exists(path));

        await store.InitializeAsync();

        Assert.True(File.Exists(path));
        Assert.Equal(InvitationDecisionReason.BindingMissing,
            (await store.CheckBoundIdentityAsync("google", "subject")).Reason);
        var invitationId = await store.CreateInvitationAsync("provisioned@example.test");
        Assert.True(await store.ApproveInvitationAsync(invitationId));
        Assert.Equal(InvitationDecisionReason.NewBinding,
            (await store.BindVerifiedIdentityAsync("google", "subject", "provisioned@example.test")).Reason);
    }

    [Fact]
    public async Task Binding_constraint_failure_denies_and_rolls_back_the_redeemable_slot()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.ApproveAsync("blocked@example.test");
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fixture.Path,
            Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync();
            using var block = connection.CreateCommand();
            block.CommandText = """
                CREATE TRIGGER block_binding BEFORE INSERT ON identity_bindings
                BEGIN SELECT RAISE(ABORT, 'blocked'); END;
                """;
            await block.ExecuteNonQueryAsync();
        }

        Assert.Equal(InvitationDecisionReason.IdentityConflict,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "stable", "blocked@example.test")).Reason);

        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fixture.Path,
            Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync();
            using var unblock = connection.CreateCommand();
            unblock.CommandText = "DROP TRIGGER block_binding;";
            await unblock.ExecuteNonQueryAsync();
        }
        Assert.Equal(InvitationDecisionReason.NewBinding,
            (await fixture.Store.BindVerifiedIdentityAsync("google", "stable", "blocked@example.test")).Reason);
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(string directoryPath)
        {
            DirectoryPath = directoryPath;
            Path = System.IO.Path.Combine(directoryPath, "invitations.db");
            Clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
            Store = new SqliteInvitationStore(Path, Clock);
        }

        public string DirectoryPath { get; }
        public string Path { get; }
        public ManualTimeProvider Clock { get; }
        public SqliteInvitationStore Store { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "oidc-starter-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var fixture = new Fixture(directory);
            await fixture.Store.InitializeAsync();
            return fixture;
        }

        public async Task<Guid> ApproveAsync(string email)
        {
            var id = await Store.CreateInvitationAsync(email);
            Assert.True(await Store.ApproveInvitationAsync(id));
            return id;
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }

    private sealed class ManualTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset current = initialTime;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }
}
