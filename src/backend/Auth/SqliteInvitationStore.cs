using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Backend.Auth;

// Sample-only persistence. Call InitializeAsync explicitly during provisioning, not app startup.
internal sealed class SqliteInvitationStore
{
    private readonly string connectionString;
    private readonly string provisioningConnectionString;
    private readonly TimeProvider timeProvider;

    public SqliteInvitationStore(string databasePath, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !Path.IsPathFullyQualified(databasePath))
        {
            throw new ArgumentException("An absolute SQLite database file path is required.", nameof(databasePath));
        }

        var connectionOptions = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 10
        };
        connectionString = connectionOptions.ToString();
        connectionOptions.Mode = SqliteOpenMode.ReadWriteCreate;
        provisioningConnectionString = connectionOptions.ToString();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken, provision: true);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction, """
            CREATE TABLE IF NOT EXISTS invitations (
                id TEXT PRIMARY KEY,
                canonical_email TEXT NOT NULL,
                redeemable_email TEXT UNIQUE,
                created_at INTEGER NOT NULL,
                approved_at INTEGER,
                expires_at INTEGER,
                revoked_at INTEGER,
                CHECK (redeemable_email IS NULL OR redeemable_email = canonical_email),
                CHECK (expires_at IS NULL OR expires_at > created_at)
            );
            CREATE TABLE IF NOT EXISTS identity_bindings (
                invitation_id TEXT PRIMARY KEY REFERENCES invitations(id) ON DELETE RESTRICT,
                provider_id TEXT NOT NULL CHECK (provider_id IN ('google', 'github')),
                provider_subject TEXT NOT NULL,
                bound_at INTEGER NOT NULL,
                UNIQUE (provider_id, provider_subject)
            );
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    // Operator-facing persistence only; no HTTP endpoint is registered in this slice.
    public async Task<Guid> CreateInvitationAsync(
        string email,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        if (!InvitationEmail.TryCanonicalize(email, out var canonicalEmail))
        {
            throw new ArgumentException("A valid unambiguous email address is required.", nameof(email));
        }

        var now = timeProvider.GetUtcNow();
        if (expiresAt is { } expiration && expiration <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiration must be in the future.");
        }

        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(deferred: false);
        // Expiry closes only the unredeemed slot; it never revokes a binding.
        using (var closeExpired = Command(connection, transaction, """
            UPDATE invitations SET redeemable_email = NULL
            WHERE redeemable_email = $email AND expires_at IS NOT NULL AND expires_at <= $now;
            """))
        {
            closeExpired.Parameters.AddWithValue("$email", canonicalEmail);
            closeExpired.Parameters.AddWithValue("$now", now.UtcTicks);
            await closeExpired.ExecuteNonQueryAsync(cancellationToken);
        }

        var id = Guid.NewGuid();
        using (var insert = Command(connection, transaction, """
            INSERT INTO invitations (id, canonical_email, redeemable_email, created_at, expires_at)
            VALUES ($id, $email, $email, $created, $expires);
            """))
        {
            insert.Parameters.AddWithValue("$id", id.ToString("N"));
            insert.Parameters.AddWithValue("$email", canonicalEmail);
            insert.Parameters.AddWithValue("$created", now.UtcTicks);
            insert.Parameters.AddWithValue("$expires", expiresAt is { } expiry ? expiry.UtcTicks : DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return id;
    }

    public async Task<bool> ApproveInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var command = Command(connection, null, """
            UPDATE invitations SET approved_at = $now
            WHERE id = $id AND approved_at IS NULL AND revoked_at IS NULL
              AND redeemable_email IS NOT NULL
              AND (expires_at IS NULL OR expires_at > $now);
            """);
        command.Parameters.AddWithValue("$id", invitationId.ToString("N"));
        command.Parameters.AddWithValue("$now", timeProvider.GetUtcNow().UtcTicks);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var command = Command(connection, null, """
            UPDATE invitations SET revoked_at = $now, redeemable_email = NULL
            WHERE id = $id AND revoked_at IS NULL;
            """);
        command.Parameters.AddWithValue("$id", invitationId.ToString("N"));
        command.Parameters.AddWithValue("$now", timeProvider.GetUtcNow().UtcTicks);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<InvitationDecision> CheckBoundIdentityAsync(
        string providerId,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidateIdentity(providerId, providerSubject, out var provider, out var subject);
        if (invalid is { } denial)
        {
            return denial;
        }

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            return await FindBindingAsync(connection, null, provider, subject, cancellationToken)
                ?? InvitationDecision.Deny(InvitationDecisionReason.BindingMissing);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return InvitationDecision.Deny(InvitationDecisionReason.StorageUnavailable);
        }
    }

    // The caller must obtain verifiedProviderEmail from the provider. This store does not verify it.
    public async Task<InvitationDecision> BindVerifiedIdentityAsync(
        string providerId,
        string providerSubject,
        string verifiedProviderEmail,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidateIdentity(providerId, providerSubject, out var provider, out var subject);
        if (invalid is { } denial)
        {
            return denial;
        }

        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            // BeginTransaction takes the writer reservation before any read, serializing first binding.
            using var transaction = connection.BeginTransaction(deferred: false);
            var bound = await FindBindingAsync(connection, transaction, provider, subject, cancellationToken);
            if (bound is { } existing)
            {
                return existing;
            }

            if (!InvitationEmail.TryCanonicalize(verifiedProviderEmail, out var email))
            {
                return InvitationDecision.Deny(InvitationDecisionReason.InvalidEmail);
            }

            var now = timeProvider.GetUtcNow().UtcTicks;
            Guid invitationId;
            using (var lookup = Command(connection, transaction, """
                SELECT id, approved_at, expires_at, revoked_at, redeemable_email
                FROM invitations WHERE canonical_email = $email
                ORDER BY (redeemable_email IS NULL), created_at DESC, rowid DESC LIMIT 1;
                """))
            {
                lookup.Parameters.AddWithValue("$email", email);
                await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return InvitationDecision.Deny(InvitationDecisionReason.InvitationMissing);
                }

                invitationId = Guid.ParseExact(reader.GetString(0), "N");
                if (!reader.IsDBNull(3))
                {
                    return InvitationDecision.Deny(InvitationDecisionReason.Revoked);
                }
                if (reader.IsDBNull(4))
                {
                    return InvitationDecision.Deny(InvitationDecisionReason.AlreadyBound);
                }
                if (reader.IsDBNull(1))
                {
                    return InvitationDecision.Deny(InvitationDecisionReason.ApprovalRequired);
                }
                if (!reader.IsDBNull(2) && reader.GetInt64(2) <= now)
                {
                    return InvitationDecision.Deny(InvitationDecisionReason.Expired);
                }
            }

            using (var closeSlot = Command(connection, transaction, """
                UPDATE invitations SET redeemable_email = NULL
                WHERE id = $id AND redeemable_email = $email AND revoked_at IS NULL
                  AND approved_at IS NOT NULL AND (expires_at IS NULL OR expires_at > $now);
                """))
            {
                closeSlot.Parameters.AddWithValue("$id", invitationId.ToString("N"));
                closeSlot.Parameters.AddWithValue("$email", email);
                closeSlot.Parameters.AddWithValue("$now", now);
                if (await closeSlot.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    return InvitationDecision.Deny(InvitationDecisionReason.IdentityConflict);
                }
            }

            using (var insert = Command(connection, transaction, """
                INSERT INTO identity_bindings (invitation_id, provider_id, provider_subject, bound_at)
                VALUES ($id, $provider, $subject, $now);
                """))
            {
                insert.Parameters.AddWithValue("$id", invitationId.ToString("N"));
                insert.Parameters.AddWithValue("$provider", provider);
                insert.Parameters.AddWithValue("$subject", subject);
                insert.Parameters.AddWithValue("$now", now);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
            return InvitationDecision.Allow(InvitationDecisionReason.NewBinding, invitationId);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return InvitationDecision.Deny(InvitationDecisionReason.IdentityConflict);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return InvitationDecision.Deny(InvitationDecisionReason.StorageUnavailable);
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken, bool provision = false)
    {
        var connection = new SqliteConnection(provision ? provisioningConnectionString : connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<InvitationDecision?> FindBindingAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string provider,
        string subject,
        CancellationToken cancellationToken)
    {
        using var command = Command(connection, transaction, """
            SELECT b.invitation_id, i.revoked_at
            FROM identity_bindings b JOIN invitations i ON i.id = b.invitation_id
            WHERE b.provider_id = $provider AND b.provider_subject = $subject;
            """);
        command.Parameters.AddWithValue("$provider", provider);
        command.Parameters.AddWithValue("$subject", subject);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return reader.IsDBNull(1)
            ? InvitationDecision.Allow(InvitationDecisionReason.BoundIdentity, Guid.ParseExact(reader.GetString(0), "N"))
            : InvitationDecision.Deny(InvitationDecisionReason.Revoked);
    }

    private static InvitationDecision? ValidateIdentity(
        string? providerId,
        string? providerSubject,
        out string provider,
        out string subject)
    {
        provider = string.Empty;
        subject = string.Empty;
        if (string.Equals(providerId, "google", StringComparison.OrdinalIgnoreCase))
        {
            provider = "google";
        }
        else if (string.Equals(providerId, "github", StringComparison.OrdinalIgnoreCase))
        {
            provider = "github";
        }
        else
        {
            return InvitationDecision.Deny(InvitationDecisionReason.UnsupportedProvider);
        }

        if (string.IsNullOrWhiteSpace(providerSubject)
            || providerSubject.Length > 512
            || providerSubject != providerSubject.Trim()
            || providerSubject.Any(char.IsControl))
        {
            return InvitationDecision.Deny(InvitationDecisionReason.InvalidSubject);
        }

        if (provider == "github")
        {
            if (!ulong.TryParse(providerSubject, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId)
                || numericId == 0)
            {
                return InvitationDecision.Deny(InvitationDecisionReason.InvalidSubject);
            }
            subject = numericId.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            subject = providerSubject;
        }

        return null;
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }
}
