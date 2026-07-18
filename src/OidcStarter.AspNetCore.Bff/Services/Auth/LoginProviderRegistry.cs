using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed record LoginProviderDescriptor(
    string Id,
    string DisplayName,
    string AuthenticationScheme,
    bool IsDefault);

internal sealed class LoginProviderRegistry
{
    private readonly Dictionary<string, LoginProviderDescriptor> providersById;

    public LoginProviderRegistry(IEnumerable<LoginProviderDescriptor> providers)
    {
        Providers = providers
            .OrderBy(static provider => provider.Id, StringComparer.Ordinal)
            .ToArray();

        if (Providers.Count == 0)
        {
            throw new InvalidOperationException("At least one login provider must be registered.");
        }

        if (Providers.Any(static provider =>
                string.IsNullOrWhiteSpace(provider.Id)
                || provider.Id != provider.Id.ToLowerInvariant()
                || string.IsNullOrWhiteSpace(provider.DisplayName)
                || string.IsNullOrWhiteSpace(provider.AuthenticationScheme)))
        {
            throw new InvalidOperationException("Login provider registrations must contain lowercase ids, display names, and authentication schemes.");
        }

        providersById = new Dictionary<string, LoginProviderDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            if (!providersById.TryAdd(provider.Id, provider))
            {
                throw new InvalidOperationException($"A login provider with id '{provider.Id}' is already registered.");
            }
        }

        DefaultProvider = Providers.SingleOrDefault(static provider => provider.IsDefault)
            ?? throw new InvalidOperationException("Exactly one login provider must be registered as the default.");

        if (Providers.Count(static provider => provider.IsDefault) != 1)
        {
            throw new InvalidOperationException("Exactly one login provider must be registered as the default.");
        }
    }

    public IReadOnlyList<LoginProviderDescriptor> Providers { get; }

    public LoginProviderDescriptor DefaultProvider { get; }

    public bool TryGetProvider(string? providerId, out LoginProviderDescriptor provider)
    {
        if (!string.IsNullOrWhiteSpace(providerId)
            && providersById.TryGetValue(providerId, out var registeredProvider))
        {
            provider = registeredProvider;
            return true;
        }

        provider = null!;
        return false;
    }

    public static LoginProviderRegistry CreateDefault()
        => new(
        [
            new LoginProviderDescriptor(
                "oidc",
                "OpenID Connect",
                OpenIdConnectDefaults.AuthenticationScheme,
                true)
        ]);
}

