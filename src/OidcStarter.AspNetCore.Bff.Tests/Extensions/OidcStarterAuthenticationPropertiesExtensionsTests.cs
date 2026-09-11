using Microsoft.AspNetCore.Authentication;
using OidcStarter.AspNetCore.Bff.Extensions;
using OidcStarter.AspNetCore.Bff.Models.Auth;
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

    [Fact]
    public void TryGetOidcStarterValidatedOidcSignInMetadata_returns_all_persisted_values()
    {
        var properties = new AuthenticationProperties();
        LoginProviderAuthenticationProperties.SetValidatedOidcSignInMetadata(
            properties,
            new OidcStarterValidatedOidcSignInMetadata(
                "oidc",
                "OpenIdConnect",
                "https://identity.example.test",
                "raw-subject",
                "upstream-session",
                "bff-client"));

        var found = properties.TryGetOidcStarterValidatedOidcSignInMetadata(out var metadata);

        Assert.True(found);
        Assert.Equal("oidc", metadata?.ProviderId);
        Assert.Equal("OpenIdConnect", metadata?.AuthenticationScheme);
        Assert.Equal("https://identity.example.test", metadata?.Issuer);
        Assert.Equal("raw-subject", metadata?.Subject);
        Assert.Equal("upstream-session", metadata?.SessionId);
        Assert.Equal("bff-client", metadata?.ClientId);
    }

    [Fact]
    public void TryGetOidcStarterValidatedOidcSignInMetadata_keeps_a_missing_session_id_optional()
    {
        var properties = new AuthenticationProperties();
        LoginProviderAuthenticationProperties.SetValidatedOidcSignInMetadata(
            properties,
            new OidcStarterValidatedOidcSignInMetadata(
                "oidc",
                "OpenIdConnect",
                "https://identity.example.test",
                "raw-subject",
                null,
                "bff-client"));

        var found = properties.TryGetOidcStarterValidatedOidcSignInMetadata(out var metadata);

        Assert.True(found);
        Assert.Null(metadata?.SessionId);
    }

    [Fact]
    public void TryGetOidcStarterValidatedOidcSignInMetadata_returns_false_for_incomplete_metadata()
    {
        var properties = new AuthenticationProperties();
        LoginProviderAuthenticationProperties.SetValidatedOidcSignInMetadata(
            properties,
            new OidcStarterValidatedOidcSignInMetadata(
                "oidc",
                "OpenIdConnect",
                "https://identity.example.test",
                string.Empty,
                null,
                "bff-client"));

        var found = properties.TryGetOidcStarterValidatedOidcSignInMetadata(out var metadata);

        Assert.False(found);
        Assert.Null(metadata);
    }

    [Fact]
    public void TryGetOidcStarterValidatedOidcSignInMetadata_returns_false_for_unrelated_properties()
    {
        var properties = new AuthenticationProperties();

        var found = properties.TryGetOidcStarterValidatedOidcSignInMetadata(out var metadata);

        Assert.False(found);
        Assert.Null(metadata);
    }
}
