using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed class ValidatedOidcSignInMetadataOptionsPostConfigure
    : IPostConfigureOptions<OpenIdConnectOptions>
{
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        var consumerOnTokenValidated = options.Events.OnTokenValidated;
        options.Events.OnTokenValidated = async context =>
        {
            var hasMetadataCandidate = ValidatedOidcSignInMetadataCapture.TryCreateCandidate(
                context,
                options.ClientId,
                out var metadataCandidate);

            await consumerOnTokenValidated(context);

            if (context.Result is not null
                || !hasMetadataCandidate
                || metadataCandidate is null
                || context.Properties is null)
            {
                return;
            }

            ValidatedOidcSignInMetadataCapture.Persist(context.Properties, metadataCandidate);
        };
    }
}
