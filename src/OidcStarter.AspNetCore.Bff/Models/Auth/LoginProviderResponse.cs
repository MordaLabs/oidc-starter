namespace OidcStarter.AspNetCore.Bff.Models.Auth;

public sealed record LoginProviderResponse(
    string Id,
    string DisplayName,
    bool IsDefault,
    string LoginUrl);
