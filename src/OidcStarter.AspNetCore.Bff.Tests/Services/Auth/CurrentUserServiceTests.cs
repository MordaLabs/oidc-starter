using System.Security.Claims;
using Microsoft.Extensions.Options;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Configuration;
using OidcStarter.AspNetCore.Bff.Services.Auth;

namespace OidcStarter.AspNetCore.Bff.Tests.Services.Auth;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void GetCurrentUser_returns_null_for_anonymous_user()
    {
        var service = CreateService([]);

        var currentUser = service.GetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Null(currentUser);
    }

    [Fact]
    public void GetCurrentUser_returns_deduplicated_sorted_roles_from_all_mappers()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-123"),
            new Claim("name", "Test User"),
            new Claim("email", "test@example.local")
        ], authenticationType: "cookie"));
        var service = CreateService(
        [
            new StaticRoleMapper("zeta", "admin"),
            new StaticRoleMapper("admin", "reader")
        ]);

        var currentUser = service.GetCurrentUser(principal, accessToken: "token-value");

        Assert.NotNull(currentUser);
        Assert.Equal("user-123", currentUser.Sub);
        Assert.Equal("Test User", currentUser.Name);
        Assert.Equal("test@example.local", currentUser.Email);
        Assert.Equal(["admin", "reader", "zeta"], currentUser.Roles);
    }

    private static CurrentUserService CreateService(IEnumerable<IOidcStarterRoleMapper> roleMappers)
        => new(
            Options.Create(new OidcStarterBffOptions()),
            roleMappers);

    private sealed class StaticRoleMapper(params string[] roles) : IOidcStarterRoleMapper
    {
        public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
        {
            Assert.Equal("token-value", context.AccessToken);
            return roles;
        }
    }
}
