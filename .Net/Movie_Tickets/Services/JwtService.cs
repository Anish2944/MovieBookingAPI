using Microsoft.IdentityModel.Tokens;
using Movie_Tickets.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class JwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration config)
    {
        _secret = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
        _issuer = config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
        _audience = config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role ?? ""),
            new Claim(ClaimTypes.Name, user.Name ?? "")
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // 👇 Debug logging
        Console.WriteLine("Generated JWT Payload:");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(token.Payload));

        return jwt;
    }

}
