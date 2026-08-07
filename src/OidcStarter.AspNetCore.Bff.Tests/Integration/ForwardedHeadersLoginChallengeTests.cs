using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace OidcStarter.AspNetCore.Bff.Tests.Integration;

public sealed class ForwardedHeadersLoginChallengeTests
{
    [Fact]
    public async Task Trusted_forwarded_public_origin_is_used_for_google_callback_uri()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Starter:AllowedForwardedHosts:0"] = "demo.example.com",
            ["Starter:KnownForwardedProxies:0"] = "127.0.0.1",
            ["Oidc:Authority"] = "https://identity.example.test",
            ["Oidc:ClientId"] = "test-oidc-client-id",
            ["Oidc:ClientSecret"] = "test-oidc-client-secret",
            ["Google:ClientId"] = "test-google-client-id",
            ["Google:ClientSecret"] = "test-google-client-secret"
        });
        builder.Services.AddOidcStarterBff(builder.Configuration);
        builder.Services.AddOidcStarterGoogle(builder.Configuration.GetSection("Google"));

        await using var app = builder.Build();
        app.UseOidcStarterBff();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Host", "demo.example.com");

        using var response = await client.GetAsync("/api/auth/login/google");

        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but received {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var authorizationRedirect = Assert.IsType<Uri>(response.Headers.Location);
        var query = QueryHelpers.ParseQuery(authorizationRedirect.Query);
        Assert.Equal("https://demo.example.com/signin-google", query["redirect_uri"].Single());
    }
}
