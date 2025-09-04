// Common/ClaimsExtensions.cs
using System.Security.Claims;

namespace Movie_Tickets.Common;
public static class ClaimsExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue("email");
}
