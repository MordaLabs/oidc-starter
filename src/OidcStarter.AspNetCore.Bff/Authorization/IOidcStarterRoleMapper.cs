namespace OidcStarter.AspNetCore.Bff.Authorization;

public interface IOidcStarterRoleMapper
{
    IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context);
}
