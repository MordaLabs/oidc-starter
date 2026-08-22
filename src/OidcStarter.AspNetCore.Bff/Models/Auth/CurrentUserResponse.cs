namespace OidcStarter.AspNetCore.Bff.Models.Auth;

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? Sub,
    string? Name,
    string? Username,
    string? Email)
{
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    public ExternalIdentityResponse? ExternalIdentity { get; init; }
}
