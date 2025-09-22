// Common/ClaimsExtensions.cs
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
namespace Movie_Tickets.Common;
public static class ClaimsExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
        => int.TryParse(
               user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub),
               out var id) ? id : null;

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email)
           ?? user.FindFirstValue(JwtRegisteredClaimNames.Email);
}
