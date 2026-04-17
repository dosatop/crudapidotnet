using System.Security.Claims;

namespace CrudApi.Extensions;

public static class UserExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);

        if (claim == null)
            throw new UnauthorizedAccessException("User ID not found");

        return int.Parse(claim.Value);
    }
}