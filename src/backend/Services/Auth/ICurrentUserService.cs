using System.Security.Claims;
using Backend.Models.Auth;

namespace Backend.Services.Auth;

public interface ICurrentUserService
{
    CurrentUserResponse? GetCurrentUser(ClaimsPrincipal user);
}
