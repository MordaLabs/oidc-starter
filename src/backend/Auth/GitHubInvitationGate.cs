using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Backend.Auth;

internal static class GitHubInvitationGate
{
    public static IServiceCollection AddGitHubInvitationGate(this IServiceCollection services, string? databasePath)
    {
        var store = new SqliteInvitationStore(databasePath!);
        services.TryAddSingleton(store);
        services.AddSingleton<IPostConfigureOptions<GitHubAuthenticationOptions>, GitHubInvitationGatePostConfigure>();
        return services;
    }
}

internal sealed class GitHubInvitationGatePostConfigure(SqliteInvitationStore store)
    : IPostConfigureOptions<GitHubAuthenticationOptions>
{
    private const string Scheme = "OidcStarter.GitHub";
    private const string DenialPath = "/?login=unavailable";
    private const int MaxEmailResponseBytes = 65536;
    private static readonly object EvidenceKey = new();

    public void PostConfigure(string? name, GitHubAuthenticationOptions options)
    {
        if (name != Scheme) return;
        if (options.EventsType is not null)
            throw new InvalidOperationException("GitHub invitation enforcement cannot compose with EventsType.");

        var emailsEndpoint = ResolveTrustedEmailsEndpoint(options)
            ?? throw new InvalidOperationException("GitHub invitation enforcement requires a trusted OAuth endpoint topology.");

        // The pinned handler fetches primary email before CreatingTicket when /user has no email.
        // That fallback ignores verified and would also fetch unnecessarily for returning users.
        options.UserEmailsEndpoint = string.Empty;
        var creatingTicket = options.Events.OnCreatingTicket;
        var ticketReceived = options.Events.OnTicketReceived;
        var remoteFailure = options.Events.OnRemoteFailure;

        options.Events.OnCreatingTicket = async context =>
        {
            await creatingTicket(context);
            context.HttpContext.Items[EvidenceKey] = new Evidence(
                ReadSubject(context.User), context.AccessToken, context.Backchannel, emailsEndpoint);
        };

        options.Events.OnTicketReceived = async context =>
        {
            try
            {
                await ticketReceived(context);
                if (context.Result is { Handled: true } or { Skipped: true }) return;
                if (context.Result is { Failure: not null } or { None: true })
                {
                    Deny(context);
                    return;
                }

                context.HttpContext.Items.TryGetValue(EvidenceKey, out var item);
                if (item is not Evidence evidence
                    || !await IsAllowedAsync(evidence, context.Principal, store,
                        context.HttpContext.RequestAborted))
                {
                    Deny(context);
                }
            }
            finally
            {
                context.HttpContext.Items.Remove(EvidenceKey);
            }
        };

        options.Events.OnRemoteFailure = async context =>
        {
            try
            {
                await remoteFailure(context);
                if (context.Result is not ({ Handled: true } or { Skipped: true })) Deny(context);
            }
            finally
            {
                context.HttpContext.Items.Remove(EvidenceKey);
            }
        };
    }

    internal static string? ReadSubject(JsonElement user)
    {
        if (user.ValueKind != JsonValueKind.Object
            || user.EnumerateObject().Count(static property => property.NameEquals("id")) != 1
            || !user.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.Number
            || !id.TryGetUInt64(out var number)
            || number == 0)
            return null;
        return number.ToString(CultureInfo.InvariantCulture);
    }

    internal static async Task<bool> IsAllowedAsync(Evidence evidence, ClaimsPrincipal? principal,
        SqliteInvitationStore store, CancellationToken cancellationToken = default)
    {
        if (evidence.Subject is null
            || principal?.FindAll(ClaimTypes.NameIdentifier).Select(static claim => claim.Value).ToArray()
                is not { Length: 1 } subjects
            || subjects[0] != evidence.Subject)
            return false;

        var bound = await store.CheckBoundIdentityAsync("github", evidence.Subject, cancellationToken);
        if (bound.Allowed) return true;
        if (bound.Reason != InvitationDecisionReason.BindingMissing) return false;

        var email = await GetVerifiedPrimaryEmailAsync(evidence, cancellationToken);
        return email is not null
            && (await store.BindVerifiedIdentityAsync("github", evidence.Subject, email, cancellationToken)).Allowed;
    }

    internal static async Task<string?> GetVerifiedPrimaryEmailAsync(Evidence evidence,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(evidence.AccessToken)) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, evidence.EmailsEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", evidence.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("oidc-starter-sample");
            using var response = await evidence.Backchannel.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode
                || !IsExpectedFinalEndpoint(response.RequestMessage?.RequestUri, evidence.EmailsEndpoint)
                || response.Content.Headers.ContentLength > MaxEmailResponseBytes)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) != 0)
            {
                if (buffer.Length + read > MaxEmailResponseBytes) return null;
                buffer.Write(chunk, 0, read);
            }

            using var document = JsonDocument.Parse(buffer.ToArray());
            return ReadVerifiedPrimaryEmail(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    internal static string? ReadVerifiedPrimaryEmail(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() is < 1 or > 100) return null;
        string? primary = null;
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || item.EnumerateObject().Count(static p => p.NameEquals("email")) != 1
                || item.EnumerateObject().Count(static p => p.NameEquals("primary")) != 1
                || item.EnumerateObject().Count(static p => p.NameEquals("verified")) != 1
                || !item.TryGetProperty("email", out var emailValue)
                || emailValue.ValueKind != JsonValueKind.String
                || !InvitationEmail.TryCanonicalize(emailValue.GetString(), out var email)
                || !item.TryGetProperty("primary", out var primaryValue)
                || primaryValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !item.TryGetProperty("verified", out var verifiedValue)
                || verifiedValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return null;

            if (primaryValue.ValueKind == JsonValueKind.True)
            {
                if (primary is not null || verifiedValue.ValueKind != JsonValueKind.True) return null;
                primary = email;
            }
        }
        return primary;
    }

    private static void Deny(TicketReceivedContext context)
    {
        context.HandleResponse();
        context.Response.Redirect(DenialPath);
    }

    private static void Deny(RemoteFailureContext context)
    {
        context.HandleResponse();
        context.Response.Redirect(DenialPath);
    }

    internal sealed record Evidence(string? Subject, string? AccessToken, HttpClient Backchannel,
        Uri EmailsEndpoint);

    internal static Uri? ResolveTrustedEmailsEndpoint(GitHubAuthenticationOptions options)
    {
        if (!TryEndpoint(options.AuthorizationEndpoint, out var authorization)
            || !TryEndpoint(options.TokenEndpoint, out var token)
            || !TryEndpoint(options.UserInformationEndpoint, out var user)
            || !TryEndpoint(options.UserEmailsEndpoint, out var emails))
            return null;

        if (string.IsNullOrWhiteSpace(options.EnterpriseDomain))
        {
            return Matches(authorization, "github.com", "/login/oauth/authorize")
                && Matches(token, "github.com", "/login/oauth/access_token")
                && Matches(user, "api.github.com", "/user")
                && Matches(emails, "api.github.com", "/user/emails")
                ? emails : null;
        }

        var domain = options.EnterpriseDomain;
        if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(domain.Contains("://", StringComparison.Ordinal) ? domain : "https://" + domain,
                UriKind.Absolute, out var enterprise)
            || enterprise.Scheme != Uri.UriSchemeHttps
            || !enterprise.IsDefaultPort
            || enterprise.HostNameType != UriHostNameType.Dns
            || !enterprise.Host.Contains('.')
            || enterprise.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || enterprise.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || enterprise.AbsolutePath != "/"
            || enterprise.UserInfo.Length != 0
            || enterprise.Query.Length != 0
            || enterprise.Fragment.Length != 0)
            return null;

        var host = enterprise.Host;
        return Matches(authorization, host, "/login/oauth/authorize")
            && Matches(token, host, "/login/oauth/access_token")
            && Matches(user, host, "/api/v3/user")
            && Matches(emails, host, "/api/v3/user/emails")
            ? emails : null;
    }

    private static bool TryEndpoint(string? value, out Uri endpoint)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps
            && parsed.IsDefaultPort
            && parsed.UserInfo.Length == 0
            && parsed.Query.Length == 0
            && parsed.Fragment.Length == 0)
        {
            endpoint = parsed;
            return true;
        }
        endpoint = null!;
        return false;
    }

    private static bool Matches(Uri endpoint, string host, string path)
        => endpoint.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            && endpoint.AbsolutePath.Equals(path, StringComparison.Ordinal);

    private static bool IsExpectedFinalEndpoint(Uri? actual, Uri expected)
        => actual is { IsAbsoluteUri: true }
            && actual.Scheme.Equals(expected.Scheme, StringComparison.OrdinalIgnoreCase)
            && actual.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase)
            && actual.Port == expected.Port
            && actual.UserInfo.Equals(expected.UserInfo, StringComparison.Ordinal)
            && actual.AbsolutePath.Equals(expected.AbsolutePath, StringComparison.Ordinal)
            && actual.Query.Equals(expected.Query, StringComparison.Ordinal)
            && actual.Fragment.Equals(expected.Fragment, StringComparison.Ordinal);
}
