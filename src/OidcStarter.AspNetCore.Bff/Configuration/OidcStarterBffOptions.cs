namespace OidcStarter.AspNetCore.Bff.Configuration;

using Microsoft.AspNetCore.Http;

public sealed class OidcStarterBffOptions
{
    public const string SectionName = "Starter";

    public string FrontendOrigin { get; set; } = "http://localhost:4200";

    public string[] AllowedForwardedHosts { get; set; } = ["localhost"];

    public string[] KnownForwardedProxies { get; set; } = [];

    public string[] KnownForwardedNetworks { get; set; } = [];

    public string CookieName { get; set; } = "__Host-oidc-starter-bff";

    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    public bool SlidingExpiration { get; set; } = true;

    public SameSiteMode CookieSameSite { get; set; } = SameSiteMode.None;

    public bool RequireAntiforgeryToken { get; set; }

    public string AntiforgeryHeaderName { get; set; } = "X-XSRF-TOKEN";

    public string AntiforgeryCookieName { get; set; } = "__Host-oidc-starter-bff-af";

    public string AntiforgeryRequestTokenCookieName { get; set; } = "XSRF-TOKEN";
}
