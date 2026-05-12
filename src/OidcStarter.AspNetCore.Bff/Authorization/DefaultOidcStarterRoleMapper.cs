using System.Security.Claims;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Authorization;

internal sealed class DefaultOidcStarterRoleMapper(
    IOptions<OidcStarterBffOptions> bffOptions) : IOidcStarterRoleMapper
{
    public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
    {
        var roleClaimTypes = new[]
            {
                bffOptions.Value.RoleClaimType
            }
            .Concat(bffOptions.Value.AdditionalRoleClaimTypes)
            .Where(static claimType => !string.IsNullOrWhiteSpace(claimType))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return roleClaimTypes
            .SelectMany(context.Principal.FindAll)
            .Select(static claim => claim.Value);
    }
}
