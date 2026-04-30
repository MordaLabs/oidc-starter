namespace Backend.Configuration;

public sealed class StarterOptions
{
    public const string SectionName = "Starter";

    public string ApplicationName { get; set; } = "OIDC Starter API";
}
