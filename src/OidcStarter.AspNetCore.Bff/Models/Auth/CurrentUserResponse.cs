namespace OidcStarter.AspNetCore.Bff.Models.Auth;

public sealed record CurrentUserResponse(
    bool IsAuthenticated,
    string? Sub,
    string? Name,
    string? Username,
    string? Email);
