using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class LoginProviderRegistryOptionsPostConfigure(
    IEnumerable<LoginProviderDescriptor> loginProviders) : IPostConfigureOptions<OidcStarterBffOptions>
{
    public void PostConfigure(string? name, OidcStarterBffOptions options)
        => options.LoginProviders = new LoginProviderRegistry(loginProviders, options.DefaultLoginProvider);
}
