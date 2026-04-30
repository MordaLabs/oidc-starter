using Backend.Configuration;
using Backend.Models.Public;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace Backend.Controllers;

[ApiController]
[Route("api/public")]
public sealed class PublicController(
    IOptions<StarterOptions> starterOptions,
    IOptions<OidcOptions> oidcOptions) : ControllerBase
{
    [HttpGet("ping")]
    public ActionResult<PingResponse> Ping()
    {
        var starter = starterOptions.Value;
        var oidc = oidcOptions.Value;

        return Ok(new PingResponse(
            "ok",
            starter.ApplicationName,
            DateTimeOffset.UtcNow,
            !string.IsNullOrWhiteSpace(oidc.Authority)
                && !string.IsNullOrWhiteSpace(oidc.ClientId)));
    }
}
