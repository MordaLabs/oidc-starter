namespace Backend.Configuration;

public sealed class StarterOptions
{
    public const string SectionName = "Starter";

    public string ApplicationName { get; set; } = "OIDC Starter API";

    public string FrontendOrigin { get; set; } = "http://localhost:4200";

    public string[] AllowedForwardedHosts { get; set; } = ["localhost"];
}
