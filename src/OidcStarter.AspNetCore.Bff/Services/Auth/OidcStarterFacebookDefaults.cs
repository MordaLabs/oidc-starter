namespace OidcStarter.AspNetCore.Bff.Services.Auth;

internal static class OidcStarterFacebookDefaults
{
    public const string GraphApiVersion = "v26.0";
    public const string AuthenticationScheme = "OidcStarter.Facebook";
    public const string ProviderId = "facebook";
    public const string DisplayName = "Facebook";
    public const string DefaultCallbackPath = "/signin-facebook";
    public const string AuthorizationEndpoint = $"https://www.facebook.com/{GraphApiVersion}/dialog/oauth";
    public const string TokenEndpoint = $"https://graph.facebook.com/{GraphApiVersion}/oauth/access_token";
    public const string UserInformationEndpoint = $"https://graph.facebook.com/{GraphApiVersion}/me";
}
