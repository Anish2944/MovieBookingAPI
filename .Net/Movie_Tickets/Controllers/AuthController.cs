using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Movie_Tickets.Data;
using Movie_Tickets.Dtos.Auth;
using BCrypt.Net;


// Fix: Use fully qualified name for RegisterRequest in Register method to resolve ambiguity

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext context, JwtService jwt)
    {
        _context = context;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Movie_Tickets.Dtos.Auth.RegisterRequest request)
    {
        var users = _context.Set<User>();
        if (users.Any(u => u.Email == request.Email))
            return BadRequest("Email already exists");

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Customer"
        };

        users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            Name = user.Name,   // ✅ Return Name
            Role = user.Role
        });
    }


    [HttpPost("login")]
    public IActionResult Login(Movie_Tickets.Dtos.Auth.LoginRequest request) // Use fully qualified name
    {
        var users = _context.Set<User>();
        var user = users.FirstOrDefault(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        var token = _jwt.GenerateToken(user);
        return Ok(new AuthResponse { Token = token, Email = user.Email, Name = user.Name, Role = user.Role });
    }

    // Replace JwtRegisteredClaimNames.Sub with "sub" string literal in Me() method
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (userId == null)
            return Unauthorized();
        var users = _context.Set<User>();
        var user = users.Find(int.Parse(userId));
        if (user == null)
            return Unauthorized();
        return Ok(new { user.Id, user.Name, user.Email, user.Role });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // For JWT, logout is typically handled on the client side by deleting the token.
        // Optionally, you can implement token blacklisting on the server side if needed.
        var userId = User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        if (userId == null)
            return Unauthorized();

        return Ok(new { message = "Logged out successfully" });
    }
}
