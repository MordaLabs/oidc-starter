using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Security;

public sealed class CsrfOriginValidator(IOptions<OidcStarterBffOptions> bffOptions)
{
    public bool IsTrustedOrigin(HttpRequest request)
    {
        var allowedOrigins = GetAllowedOrigins(request);

        if (request.Headers.TryGetValue(HeaderNames.Origin, out var originValues))
        {
            return originValues.Count == 1
                && IsAllowedOrigin(originValues[0], allowedOrigins);
        }

        if (request.Headers.TryGetValue(HeaderNames.Referer, out var refererValues)
            && refererValues.Count == 1
            && Uri.TryCreate(refererValues[0], UriKind.Absolute, out var referer))
        {
            return IsAllowedOrigin(GetOrigin(referer), allowedOrigins);
        }

        return false;
    }

    private HashSet<string> GetAllowedOrigins(HttpRequest request)
    {
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{request.Scheme}://{request.Host}"
        };

        if (Uri.TryCreate(bffOptions.Value.FrontendOrigin, UriKind.Absolute, out var frontendOrigin))
        {
            allowedOrigins.Add(GetOrigin(frontendOrigin));
        }

        return allowedOrigins;
    }

    private static bool IsAllowedOrigin(string? origin, HashSet<string> allowedOrigins)
        => !string.IsNullOrWhiteSpace(origin)
            && allowedOrigins.Contains(origin.TrimEnd('/'));

    private static string GetOrigin(Uri uri)
        => uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host}"
            : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
}
