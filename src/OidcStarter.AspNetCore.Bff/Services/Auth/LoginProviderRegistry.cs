using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal sealed record LoginProviderDescriptor(
    string Id,
    string DisplayName,
    string AuthenticationScheme);

internal static class LoginProviderRegistration
{
    public static void Validate(string? providerId, string? displayName, string? authenticationScheme)
    {
        ValidateProviderId(providerId);

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A login provider display name is required.", nameof(displayName));
        }

        if (string.IsNullOrWhiteSpace(authenticationScheme))
        {
            throw new ArgumentException("A login provider authentication scheme is required.", nameof(authenticationScheme));
        }
    }

    public static void ValidateProviderId(string? providerId)
    {
        if (string.IsNullOrEmpty(providerId)
            || !IsRouteSafeProviderId(providerId))
        {
            throw new ArgumentException(
                "A login provider id must use lowercase ASCII letters, digits, and single hyphens without leading or trailing hyphens.",
                nameof(providerId));
        }
    }

    private static bool IsRouteSafeProviderId(string providerId)
    {
        for (var index = 0; index < providerId.Length; index++)
        {
            var character = providerId[index];
            var isLowercaseLetter = character is >= 'a' and <= 'z';
            var isDigit = character is >= '0' and <= '9';
            var isHyphen = character == '-';

            if ((!isLowercaseLetter && !isDigit && !isHyphen)
                || (index == 0 && !isLowercaseLetter && !isDigit)
                || (isHyphen && (index == providerId.Length - 1 || providerId[index - 1] == '-')))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class LoginProviderRegistry
{
    private readonly Dictionary<string, LoginProviderDescriptor> providersById;

    public LoginProviderRegistry(
        IEnumerable<LoginProviderDescriptor> providers,
        string defaultProviderId)
    {
        var registeredProviders = providers.ToArray();
        if (registeredProviders.Length == 0)
        {
            throw new InvalidOperationException("At least one login provider must be registered.");
        }

        var duplicateProviderId = registeredProviders
            .GroupBy(static provider => provider.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateProviderId is not null)
        {
            throw new InvalidOperationException($"A login provider with id '{duplicateProviderId.Key}' is already registered.");
        }

        foreach (var provider in registeredProviders)
        {
            LoginProviderRegistration.Validate(
                provider.Id,
                provider.DisplayName,
                provider.AuthenticationScheme);
        }

        var duplicateAuthenticationScheme = registeredProviders
            .GroupBy(static provider => provider.AuthenticationScheme, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateAuthenticationScheme is not null)
        {
            throw new InvalidOperationException($"An authentication scheme named '{duplicateAuthenticationScheme.Key}' is already registered for a login provider.");
        }

        LoginProviderRegistration.ValidateProviderId(defaultProviderId);

        Providers = registeredProviders
            .OrderBy(static provider => provider.Id, StringComparer.Ordinal)
            .ToArray();

        providersById = new Dictionary<string, LoginProviderDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in Providers)
        {
            providersById.Add(provider.Id, provider);
        }

        DefaultProvider = providersById.GetValueOrDefault(defaultProviderId)
            ?? throw new InvalidOperationException($"The configured default login provider '{defaultProviderId}' is not registered.");
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
                OpenIdConnectDefaults.AuthenticationScheme)
        ],
        "oidc");
}
