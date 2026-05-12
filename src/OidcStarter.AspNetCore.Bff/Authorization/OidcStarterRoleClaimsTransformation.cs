using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Configuration;

namespace OidcStarter.AspNetCore.Bff.Authorization;

internal sealed class OidcStarterRoleClaimsTransformation(
    IOptions<OidcStarterBffOptions> bffOptions,
    IEnumerable<IOidcStarterRoleMapper> roleMappers) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(principal);
        }

        var roleClaimType = bffOptions.Value.RoleClaimType;

        if (string.IsNullOrWhiteSpace(roleClaimType))
        {
            return Task.FromResult(principal);
        }

        var existingRoles = principal
            .FindAll(roleClaimType)
            .Select(static claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mappingContext = new OidcStarterRoleMappingContext(principal);
        var mappedRoles = roleMappers
            .SelectMany(mapper => mapper.GetRoles(mappingContext))
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(role => !existingRoles.Contains(role))
            .ToArray();

        if (mappedRoles.Length == 0)
        {
            return Task.FromResult(principal);
        }

        var identity = new ClaimsIdentity(
            mappedRoles.Select(role => new Claim(roleClaimType, role)),
            authenticationType: "OidcStarterRoleMapping",
            nameType: bffOptions.Value.NameClaimType,
            roleType: roleClaimType);

        principal.AddIdentity(identity);

        return Task.FromResult(principal);
    }
}
