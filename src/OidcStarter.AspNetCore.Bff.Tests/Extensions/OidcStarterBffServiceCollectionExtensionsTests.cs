using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OidcStarter.AspNetCore.Bff.Authorization;
using OidcStarter.AspNetCore.Bff.Extensions;

namespace OidcStarter.AspNetCore.Bff.Tests.Extensions;

public sealed class OidcStarterBffServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddOidcStarterBff_registers_authorization_policies_from_configuration()
    {
        using var provider = CreateServices(new Dictionary<string, string?>
        {
            ["Starter:RequiredScopes:0"] = "profile",
            ["Starter:RequiredClaims:0:Type"] = "tenant",
            ["Starter:RequiredClaims:0:Values:0"] = "academy"
        });
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var authenticatedPolicy = await policyProvider.GetPolicyAsync(OidcStarterBffPolicies.AuthenticatedUser);
        var scopePolicy = await policyProvider.GetPolicyAsync(OidcStarterBffPolicies.ConfiguredRequiredScopes);
        var claimPolicy = await policyProvider.GetPolicyAsync(OidcStarterBffPolicies.ConfiguredRequiredClaims);

        Assert.NotNull(authenticatedPolicy);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authenticatedPolicy.AuthenticationSchemes);
        Assert.NotNull(scopePolicy);
        Assert.NotNull(claimPolicy);
    }

    [Fact]
    public void AddOidcStarterRoleMapper_composes_custom_mapper_with_default_mapper()
    {
        var services = new ServiceCollection();
        services.AddOidcStarterRoleMapper<CustomRoleMapper>();
        services.AddOidcStarterBff(CreateConfiguration([]));

        using var provider = services.BuildServiceProvider();

        var mappers = provider.GetServices<IOidcStarterRoleMapper>().ToArray();
        Assert.Contains(mappers, static mapper => mapper is CustomRoleMapper);
        Assert.Contains(mappers, static mapper => mapper.GetType().Name == "DefaultOidcStarterRoleMapper");
    }

    private static ServiceProvider CreateServices(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddOidcStarterBff(CreateConfiguration(values));

        return services.BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class CustomRoleMapper : IOidcStarterRoleMapper
    {
        public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
        {
            yield return "custom";
        }
    }
}
