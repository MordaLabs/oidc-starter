using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Options;

namespace Backend.Auth;

internal static class GoogleInvitationGate
{
    public static IServiceCollection AddGoogleInvitationGate(this IServiceCollection services, string? databasePath)
    {
        // Validate at registration. Opening the existing database remains a login-time decision.
        services.AddSingleton(new SqliteInvitationStore(databasePath!));
        services.AddSingleton<IPostConfigureOptions<GoogleOptions>, GoogleInvitationGatePostConfigure>();
        return services;
    }
}

internal sealed class GoogleInvitationGatePostConfigure(SqliteInvitationStore store) : IPostConfigureOptions<GoogleOptions>
{
    private const string GoogleScheme = "OidcStarter.Google";
    private static readonly object EvidenceKey = new();
    private const string DenialPath = "/?login=unavailable";

    public void PostConfigure(string? name, GoogleOptions options)
    {
        if (name != GoogleScheme)
        {
            return;
        }

        if (options.EventsType is not null)
        {
            throw new InvalidOperationException("Google invitation enforcement cannot compose with EventsType.");
        }

        var creatingTicket = options.Events.OnCreatingTicket;
        var ticketReceived = options.Events.OnTicketReceived;
        var remoteFailure = options.Events.OnRemoteFailure;

        options.Events.OnCreatingTicket = async context =>
        {
            await creatingTicket(context);
            context.HttpContext.Items[EvidenceKey] = ReadEvidence(context.User);
        };

        options.Events.OnTicketReceived = async context =>
        {
            await ticketReceived(context);
            // Only these results stop RemoteAuthenticationHandler's automatic SignInAsync.
            if (context.Result is { Handled: true } or { Skipped: true })
            {
                return;
            }

            if (context.Result is { Failure: not null } or { None: true })
            {
                Deny(context);
                return;
            }

            context.HttpContext.Items.TryGetValue(EvidenceKey, out var value);
            var allowed = await IsAllowedAsync(value as GoogleEvidence, context.Principal,
                store, context.HttpContext.RequestAborted);

            context.HttpContext.Items.Remove(EvidenceKey);
            if (!allowed)
            {
                Deny(context);
            }
        };

        options.Events.OnRemoteFailure = async context =>
        {
            await remoteFailure(context);
            if (context.Result is not ({ Handled: true } or { Skipped: true }))
            {
                Deny(context);
            }
        };
    }

    internal static GoogleEvidence? ReadEvidence(JsonElement user)
    {
        if (user.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Duplicate identity or verification keys are ambiguous, even when the last value looks valid.
        var properties = user.EnumerateObject().Select(static property => property.Name).ToArray();
        if (new[] { "sub", "email", "email_verified", "verified_email" }
            .Any(key => properties.Count(name => name == key) > 1)
            || !user.TryGetProperty("sub", out var sub)
            || sub.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(sub.GetString()))
        {
            return null;
        }

        string? email = null;
        if (user.TryGetProperty("email", out var emailValue)
            && emailValue.ValueKind == JsonValueKind.String
            && InvitationEmail.TryCanonicalize(emailValue.GetString(), out var canonical))
        {
            email = canonical;
        }

        var verified = (user.TryGetProperty("email_verified", out var verification)
                || user.TryGetProperty("verified_email", out verification))
            && verification.ValueKind == JsonValueKind.True;
        return new GoogleEvidence(sub.GetString()!, email, verified);
    }

    internal static async Task<bool> IsAllowedAsync(GoogleEvidence? evidence, ClaimsPrincipal? principal,
        SqliteInvitationStore store, CancellationToken cancellationToken = default)
    {
        if (evidence is null || !PrincipalMatches(principal, evidence))
        {
            return false;
        }

        var bound = await store.CheckBoundIdentityAsync("google", evidence.Subject, cancellationToken);
        if (bound.Allowed)
        {
            return true;
        }

        return bound.Reason == InvitationDecisionReason.BindingMissing
            && evidence.Verified
            && evidence.Email is not null
            && (await store.BindVerifiedIdentityAsync("google", evidence.Subject,
                evidence.Email, cancellationToken)).Allowed;
    }

    private static bool PrincipalMatches(ClaimsPrincipal? principal, GoogleEvidence evidence)
    {
        var subjects = principal?.FindAll(ClaimTypes.NameIdentifier).ToArray();
        if (subjects is not { Length: 1 } || subjects[0].Value != evidence.Subject)
        {
            return false;
        }

        // A bound subject remains authoritative even when Google changes its email.
        if (evidence.Email is null)
        {
            return true;
        }

        var emails = principal!.FindAll(ClaimTypes.Email).ToArray();
        return emails is { Length: 1 }
            && InvitationEmail.TryCanonicalize(emails[0].Value, out var principalEmail)
            && principalEmail == evidence.Email;
    }

    private static void Deny(Microsoft.AspNetCore.Authentication.TicketReceivedContext context)
    {
        context.HandleResponse();
        context.Response.Redirect(DenialPath);
    }

    private static void Deny(Microsoft.AspNetCore.Authentication.RemoteFailureContext context)
    {
        context.HandleResponse();
        context.Response.Redirect(DenialPath);
    }

    internal sealed record GoogleEvidence(string Subject, string? Email, bool Verified);
}
