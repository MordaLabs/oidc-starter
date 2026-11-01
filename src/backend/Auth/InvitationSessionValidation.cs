using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace Backend.Auth;

internal static class InvitationSessionValidation
{
    private const string ProviderKey = "Backend.InvitationSession.Provider";
    private const string SubjectKey = "Backend.InvitationSession.Subject";

    public static IServiceCollection AddInvitationSessionValidation(this IServiceCollection services,
        string? databasePath, bool enforceGoogle, bool enforceGitHub)
    {
        if (!enforceGoogle && !enforceGitHub) return services;

        services.TryAddSingleton(new SqliteInvitationStore(databasePath!));
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>>(
            provider => new InvitationCookiePostConfigure(
                provider.GetRequiredService<SqliteInvitationStore>(), enforceGoogle, enforceGitHub));
        return services;
    }

    public static void Mark(AuthenticationProperties properties, string provider, string subject)
    {
        properties.Items[ProviderKey] = provider;
        properties.Items[SubjectKey] = subject;
    }

    public static bool Matches(AuthenticationProperties properties, string provider, string subject)
    {
        var hasProvider = properties.Items.TryGetValue(ProviderKey, out var markedProvider);
        var hasSubject = properties.Items.TryGetValue(SubjectKey, out var markedSubject);
        // Legacy cookies have neither marker. Their protected BFF provider property
        // and exactly one subject claim remain the migration evidence.
        return !hasProvider && !hasSubject
            || hasProvider && hasSubject && markedProvider == provider && markedSubject == subject;
    }

    public static bool HasMarker(AuthenticationProperties properties)
        => properties.Items.ContainsKey(ProviderKey) || properties.Items.ContainsKey(SubjectKey);
}

internal sealed class InvitationCookiePostConfigure(
    SqliteInvitationStore store, bool enforceGoogle, bool enforceGitHub)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name != CookieAuthenticationDefaults.AuthenticationScheme) return;
        if (options.EventsType is not null)
            throw new InvalidOperationException("Invitation session validation cannot compose with cookie EventsType.");

        var previous = options.Events.OnValidatePrincipal;
        options.Events.OnValidatePrincipal = async context =>
        {
            // Snapshot the protected ticket before a consumer delegate can edit it.
            var ticketProperties = context.Properties;
            string? ticketProvider = null;
            var hasTicketProvider = ticketProperties is not null
                && ticketProperties.TryGetOidcStarterLoginProviderId(out ticketProvider);
            var ticketMarked = ticketProperties is not null
                && InvitationSessionValidation.HasMarker(ticketProperties);
            var ticketSubjects = context.Principal?.FindAll(ClaimTypes.NameIdentifier)
                .Select(static claim => claim.Value).ToArray();
            var ticketMarkerMatches = hasTicketProvider
                && ticketSubjects is { Length: 1 }
                && InvitationSessionValidation.Matches(ticketProperties!, ticketProvider!, ticketSubjects[0]);

            await previous(context);
            if (context.Principal is null) return; // Preserve a prior rejection.

            var properties = context.Properties;
            if (!hasTicketProvider
                || string.IsNullOrWhiteSpace(ticketProvider)
                || properties is null
                || !properties.TryGetOidcStarterLoginProviderId(out var provider)
                || provider != ticketProvider
                || InvitationSessionValidation.HasMarker(properties) != ticketMarked)
            {
                await RejectAsync(context);
                return;
            }

            // These IDs are written by the BFF login endpoint into protected ticket
            // properties. An unknown ID is ambiguous while enforcement is active.
            if (provider is "oidc" or "facebook")
            {
                if (ticketMarked) await RejectAsync(context);
                return;
            }

            if (provider is not ("google" or "github"))
            {
                await RejectAsync(context);
                return;
            }

            var subjects = context.Principal.FindAll(ClaimTypes.NameIdentifier).ToArray();
            var enforced = provider == "google" ? enforceGoogle : enforceGitHub;
            if ((enforced || ticketMarked)
                && (!ticketMarkerMatches
                    || subjects.Length != 1
                    || ticketSubjects![0] != subjects[0].Value
                    || !InvitationSessionValidation.Matches(properties, provider, subjects[0].Value)))
            {
                await RejectAsync(context);
                return;
            }

            if (!enforced) return;

            // Logout grants no application access. Keep a structurally valid external
            // ticket authenticated only for the package's local-only sign-out choice.
            if (IsLogoutRequest(context.HttpContext.Request)
                && context.Principal.Identity?.IsAuthenticated == true
                && IsValidLogoutSubject(provider, subjects[0].Value)) return;

            var decision = await store.CheckBoundIdentityAsync(provider, subjects[0].Value,
                context.HttpContext.RequestAborted);
            if (!decision.Allowed || decision.Reason != InvitationDecisionReason.BoundIdentity)
                await RejectAsync(context);
        };
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static bool IsLogoutRequest(HttpRequest request)
        => string.Equals(request.Method, HttpMethods.Post, StringComparison.Ordinal)
            && string.Equals(request.Path.Value, "/api/auth/logout", StringComparison.Ordinal);

    private static bool IsValidLogoutSubject(string provider, string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)
            || subject.Length > 512
            || subject != subject.Trim()
            || subject.Any(char.IsControl)) return false;

        return provider != "github"
            || ulong.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && id != 0
                && id.ToString(CultureInfo.InvariantCulture) == subject;
    }
}
