namespace OidcStarter.AspNetCore.Bff.Configuration;

public sealed class RequiredClaimOptions
{
    public string Type { get; set; } = string.Empty;

    public string[] Values { get; set; } = [];
}
