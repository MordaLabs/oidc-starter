using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using OidcStarter.AspNetCore.Bff.Models.Auth;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class ValidatedOidcSignInMetadataCapture
{
    public static bool TryCreateCandidate(
        TokenValidatedContext context,
        string? clientId,
        out OidcStarterValidatedOidcSignInMetadata? metadata)
    {
        if (context.Properties is null
            || context.SecurityToken is null
            || !LoginProviderAuthenticationProperties.TryGetLoginProviderId(context.Properties, out var providerId)
            || string.IsNullOrWhiteSpace(providerId)
            || string.IsNullOrWhiteSpace(context.Scheme.Name)
            || string.IsNullOrWhiteSpace(context.SecurityToken.Issuer)
            || string.IsNullOrWhiteSpace(clientId)
            || !TryGetRawTokenValues(context.SecurityToken, out var subject, out var sessionId))
        {
            metadata = null;
            return false;
        }

        metadata = new OidcStarterValidatedOidcSignInMetadata(
            providerId,
            context.Scheme.Name,
            context.SecurityToken.Issuer,
            subject,
            sessionId,
            clientId);
        return true;
    }

    public static void Persist(
        AuthenticationProperties properties,
        OidcStarterValidatedOidcSignInMetadata metadata)
        => LoginProviderAuthenticationProperties.SetValidatedOidcSignInMetadata(properties, metadata);

    private static bool TryGetRawTokenValues(
        JwtSecurityToken securityToken,
        out string subject,
        out string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(securityToken.Subject))
        {
            subject = string.Empty;
            sessionId = null;
            return false;
        }

        subject = securityToken.Subject;
        sessionId = securityToken.Claims.FirstOrDefault(static claim => claim.Type == "sid")?.Value;
        return true;
    }
}
