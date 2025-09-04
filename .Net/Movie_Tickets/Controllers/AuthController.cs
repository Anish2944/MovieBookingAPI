// Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;
using Movie_Tickets.Dtos.Auth;
using Movie_Tickets.Common;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    public AuthController(AppDbContext db, JwtService jwt) { _db = db; _jwt = jwt; }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object>(false, null, "Invalid input"));

        var normalized = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalized);
        if (exists) return Conflict(new ApiResponse<object>(false, null, "Email already exists"));

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "Customer"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user); // make sure it includes "sub", "email", "role"
        return Ok(new ApiResponse<AuthResponse>(true, new AuthResponse
        {
            Token = token,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role
        }));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object>(false, null, "Invalid input"));

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ApiResponse<object>(false, null, "Invalid credentials"));

        var token = _jwt.GenerateToken(user);
        return Ok(new ApiResponse<AuthResponse>(true, new AuthResponse
        {
            Token = token,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role
        }));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = User.GetUserId();
        if (id is null) return Unauthorized(new ApiResponse<object>(false, null, "Not logged in"));
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return Unauthorized(new ApiResponse<object>(false, null, "User not found"));

        return Ok(new ApiResponse<object>(true, new { user.Id, user.Name, user.Email, user.Role }));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
        => Ok(new ApiResponse<object>(true, new { message = "Logged out (client should discard JWT)" }));
}
