using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace Backend.Auth;

internal static class InvitationDatabaseProvisioning
{
    internal const string Command = "--provision-demo-invitations";

    internal static bool IsRequested(string[] args)
        => args.Any(arg => string.Equals(arg, Command, StringComparison.Ordinal)
            || arg.StartsWith(Command + "=", StringComparison.Ordinal));

    internal static async Task<int> RunAsync(
        string[] args,
        IConfiguration configuration,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (args.Length != 1 || args[0] != Command)
        {
            await error.WriteLineAsync("Provisioning requires only --provision-demo-invitations.");
            return 2;
        }

        var path = configuration["DemoInvitations:DatabasePath"];
        if (!IsUsableAbsolutePath(path))
        {
            await error.WriteLineAsync("Provisioning requires a usable, absolute DemoInvitations:DatabasePath with an existing parent directory.");
            return 2;
        }

        try
        {
            await new SqliteInvitationStore(path).InitializeAsync(cancellationToken);
            await output.WriteLineAsync("Invitation database schema provisioning completed.");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await error.WriteLineAsync("Invitation database provisioning failed; check the path, permissions, and schema.");
            return 1;
        }
    }

    private static bool IsUsableAbsolutePath([NotNullWhen(true)] string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim()) return false;
        try
        {
            if (!Path.IsPathFullyQualified(path) || string.IsNullOrEmpty(Path.GetFileName(path)))
                return false;
            var fullPath = Path.GetFullPath(path);
            return Directory.Exists(Path.GetDirectoryName(fullPath)) && !Directory.Exists(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
