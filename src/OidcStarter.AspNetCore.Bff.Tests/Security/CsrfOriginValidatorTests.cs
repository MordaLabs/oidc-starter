using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Security;

namespace OidcStarter.AspNetCore.Bff.Tests.Security;

public sealed class CsrfOriginValidatorTests
{
    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("https://api.example.com")]
    public void IsTrustedOrigin_accepts_configured_frontend_or_current_backend_origin(string origin)
    {
        var request = CreateRequest();
        request.Headers.Origin = origin;

        var validator = CreateValidator();

        Assert.True(validator.IsTrustedOrigin(request));
    }

    [Fact]
    public void IsTrustedOrigin_accepts_trusted_referer_origin()
    {
        var request = CreateRequest();
        request.Headers.Referer = "http://localhost:4200/signed-out";

        var validator = CreateValidator();

        Assert.True(validator.IsTrustedOrigin(request));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("")]
    public void IsTrustedOrigin_rejects_untrusted_or_missing_origin(string? origin)
    {
        var request = CreateRequest();

        if (origin is not null)
        {
            request.Headers.Origin = origin;
        }

        var validator = CreateValidator();

        Assert.False(validator.IsTrustedOrigin(request));
    }

    private static HttpRequest CreateRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");

        return context.Request;
    }

    private static CsrfOriginValidator CreateValidator()
        => new(Options.Create(new OidcStarterBffOptions
        {
            FrontendOrigin = "http://localhost:4200"
        }));
}
