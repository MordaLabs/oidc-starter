using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OidcStarter.AspNetCore.Bff.Authorization;

namespace Backend.Controllers;

[ApiController]
[Route("api/protected")]
public sealed class ProtectedController : ControllerBase
{
    [Authorize(Policy = OidcStarterBffPolicies.AuthenticatedUser)]
    [HttpGet("ping")]
    public IActionResult Ping()
        => Ok(new
        {
            status = "ok",
            message = "Authenticated backend session is active."
        });
}
