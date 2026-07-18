using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class LoginProviderRegistryOptionsPostConfigure(
    LoginProviderRegistry loginProviderRegistry) : IPostConfigureOptions<OidcStarterBffOptions>
{
    public void PostConfigure(string? name, OidcStarterBffOptions options)
        => options.LoginProviders = loginProviderRegistry;
}

