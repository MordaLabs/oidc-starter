namespace OidcStarter.AspNetCore.Bff.Models.Auth;

public sealed record ExternalIdentityResponse(string ProviderId)
{
    public bool? EmailVerified { get; init; }

    public string? PictureUrl { get; init; }
}
