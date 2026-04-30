using System.Security.Claims;
using OidcStarter.AspNetCore.Bff.Models.Auth;

namespace OidcStarter.AspNetCore.Bff.Services.Auth;

public interface ICurrentUserService
{
    CurrentUserResponse? GetCurrentUser(ClaimsPrincipal user);
}
