using Microsoft.AspNetCore.Authentication;
using OidcStarter.AspNetCore.Bff.Extensions;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Tests.Extensions;

public sealed class OidcStarterAuthenticationPropertiesExtensionsTests
{
    [Fact]
    public void TryGetOidcStarterLoginProviderId_returns_the_persisted_login_provider_id()
    {
        var properties = new AuthenticationProperties();
        LoginProviderAuthenticationProperties.SetLoginProviderId(properties, "google");

        var found = properties.TryGetOidcStarterLoginProviderId(out var providerId);

        Assert.True(found);
        Assert.Equal("google", providerId);
    }

    [Fact]
    public void TryGetOidcStarterLoginProviderId_returns_false_when_no_login_provider_id_is_persisted()
    {
        var properties = new AuthenticationProperties();

        var found = properties.TryGetOidcStarterLoginProviderId(out var providerId);

        Assert.False(found);
        Assert.Null(providerId);
    }
}
