using System.Security.Claims;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Tests.Authorization;

public sealed class OidcStarterRoleClaimsTransformationTests
{
    [Fact]
    public async Task TransformAsync_adds_custom_mapped_roles_to_configured_role_claim_type()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("role", "existing")
        ], authenticationType: "cookie", nameType: "name", roleType: "role"));
        var transformation = new OidcStarterRoleClaimsTransformation(
            Options.Create(new OidcStarterBffOptions { RoleClaimType = "role" }),
            [new StaticRoleMapper("existing", "mapped", "mapped")]);

        var transformed = await transformation.TransformAsync(principal);

        var roles = transformed.FindAll("role").Select(static claim => claim.Value).ToArray();
        Assert.Contains("existing", roles);
        Assert.Contains("mapped", roles);
        Assert.Equal(1, roles.Count(static role => role == "mapped"));
    }

    private sealed class StaticRoleMapper(params string[] roles) : IOidcStarterRoleMapper
    {
        public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context) => roles;
    }
}
