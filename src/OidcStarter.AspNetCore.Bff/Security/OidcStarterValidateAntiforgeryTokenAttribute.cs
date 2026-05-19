using Microsoft.AspNetCore.Mvc;

namespace OidcStarter.AspNetCore.Bff.Security;

internal sealed class OidcStarterValidateAntiforgeryTokenAttribute : TypeFilterAttribute
{
    public OidcStarterValidateAntiforgeryTokenAttribute()
        : base(typeof(OidcStarterValidateAntiforgeryTokenFilter))
    {
        Order = 1000;
    }
}
