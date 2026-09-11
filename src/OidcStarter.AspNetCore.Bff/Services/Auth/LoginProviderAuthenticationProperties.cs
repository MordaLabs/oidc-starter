using Microsoft.AspNetCore.Authentication;
using OidcStarter.AspNetCore.Bff.Models.Auth;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class LoginProviderAuthenticationProperties
{
    private const string ProviderIdItemKey = "OidcStarter.AspNetCore.Bff.LoginProviderId";
    private const string ValidatedOidcProviderIdItemKey = "OidcStarter.AspNetCore.Bff.ValidatedOidc.ProviderId";
    private const string ValidatedOidcAuthenticationSchemeItemKey = "OidcStarter.AspNetCore.Bff.ValidatedOidc.AuthenticationScheme";
    private const string ValidatedOidcIssuerItemKey = "OidcStarter.AspNetCore.Bff.ValidatedOidc.Issuer";
    private const string ValidatedOidcSubjectItemKey = "OidcStarter.AspNetCore.Bff.ValidatedOidc.Subject";
    private const string ValidatedOidcSessionIdItemKey = "OidcStarter.AspNetCore.Bff.ValidatedOidc.SessionId";
    private const string ValidatedOidcClientIdItemKey = "OidcStarter.AspNetCore.Bff.ValidatedOidc.ClientId";

    public static void SetLoginProviderId(AuthenticationProperties properties, string providerId)
        => properties.Items[ProviderIdItemKey] = providerId;

    public static bool TryGetLoginProviderId(
        AuthenticationProperties properties,
        out string? providerId)
        => properties.Items.TryGetValue(ProviderIdItemKey, out providerId);

    public static void SetValidatedOidcSignInMetadata(
        AuthenticationProperties properties,
        OidcStarterValidatedOidcSignInMetadata metadata)
    {
        properties.Items[ValidatedOidcProviderIdItemKey] = metadata.ProviderId;
        properties.Items[ValidatedOidcAuthenticationSchemeItemKey] = metadata.AuthenticationScheme;
        properties.Items[ValidatedOidcIssuerItemKey] = metadata.Issuer;
        properties.Items[ValidatedOidcSubjectItemKey] = metadata.Subject;
        properties.Items[ValidatedOidcClientIdItemKey] = metadata.ClientId;

        if (metadata.SessionId is null)
        {
            properties.Items.Remove(ValidatedOidcSessionIdItemKey);
        }
        else
        {
            properties.Items[ValidatedOidcSessionIdItemKey] = metadata.SessionId;
        }
    }

    public static bool TryGetValidatedOidcSignInMetadata(
        AuthenticationProperties properties,
        out OidcStarterValidatedOidcSignInMetadata? metadata)
    {
        if (!TryGetRequiredItem(properties, ValidatedOidcProviderIdItemKey, out var providerId)
            || !TryGetRequiredItem(properties, ValidatedOidcAuthenticationSchemeItemKey, out var authenticationScheme)
            || !TryGetRequiredItem(properties, ValidatedOidcIssuerItemKey, out var issuer)
            || !TryGetRequiredItem(properties, ValidatedOidcSubjectItemKey, out var subject)
            || !TryGetRequiredItem(properties, ValidatedOidcClientIdItemKey, out var clientId))
        {
            metadata = null;
            return false;
        }

        properties.Items.TryGetValue(ValidatedOidcSessionIdItemKey, out var sessionId);
        metadata = new OidcStarterValidatedOidcSignInMetadata(
            providerId,
            authenticationScheme,
            issuer,
            subject,
            string.IsNullOrWhiteSpace(sessionId) ? null : sessionId,
            clientId);
        return true;
    }

    private static bool TryGetRequiredItem(
        AuthenticationProperties properties,
        string key,
        out string value)
    {
        if (properties.Items.TryGetValue(key, out var item)
            && !string.IsNullOrWhiteSpace(item))
        {
            value = item;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
