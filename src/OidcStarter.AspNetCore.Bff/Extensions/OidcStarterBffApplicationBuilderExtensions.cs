using Microsoft.AspNetCore.Builder;

namespace OidcStarter.AspNetCore.Bff.Extensions;

public static class OidcStarterBffApplicationBuilderExtensions
{
    public static IApplicationBuilder UseOidcStarterBff(this IApplicationBuilder app)
    {
        app.UseForwardedHeaders();
        app.UseHttpsRedirection();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
