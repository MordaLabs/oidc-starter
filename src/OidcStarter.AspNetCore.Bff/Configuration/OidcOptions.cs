namespace OidcStarter.AspNetCore.Bff.Configuration;

public sealed class OidcOptions
{
    public const string SectionName = "Oidc";

    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string CallbackPath { get; set; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    public bool RequireHttpsMetadata { get; set; } = true;

    public string[] Scopes { get; set; } = [];
}
