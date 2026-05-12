using System.Security.Claims;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Tests.Authorization;

public sealed class DefaultOidcStarterRoleMapperTests
{
    [Fact]
    public void GetRoles_reads_configured_flat_role_claim_types()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("app_role", "admin"),
            new Claim("roles", "operator"),
            new Claim(ClaimTypes.Role, "support"),
            new Claim("ignored", "nope")
        ]));
        var mapper = new DefaultOidcStarterRoleMapper(Options.Create(new OidcStarterBffOptions
        {
            RoleClaimType = "app_role",
            AdditionalRoleClaimTypes = [ClaimTypes.Role, "roles"]
        }));

        var roles = mapper.GetRoles(new OidcStarterRoleMappingContext(principal)).ToArray();

        Assert.Contains("admin", roles);
        Assert.Contains("operator", roles);
        Assert.Contains("support", roles);
        Assert.DoesNotContain("nope", roles);
    }
}
