namespace OidcStarter.AspNetCore.Bff.Models.Auth;

public sealed record OidcStarterValidatedOidcSignInMetadata(
    string ProviderId,
    string AuthenticationScheme,
    string Issuer,
    string Subject,
    string? SessionId,
    string ClientId);
