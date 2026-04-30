namespace OidcStarter.AspNetCore.Bff.Configuration;

public sealed class OidcStarterBffOptions
{
    public const string SectionName = "Starter";

    public string FrontendOrigin { get; set; } = "http://localhost:4200";

    public string[] AllowedForwardedHosts { get; set; } = ["localhost"];
}
