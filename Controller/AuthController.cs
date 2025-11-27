using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using System.Security.Claims;
using Gold_e_Shop.DTO;
using Gold_e_Shop.Model;
using Gold_e_Shop.Services;
using Microsoft.AspNetCore.Authorization.Infrastructure;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthController(AppDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    // POST /api/auth/register (опціонально: якщо хочеш дозволити створювати адміна)
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password))
        {
            return BadRequest(new { error = "Username, email and password are required" });
        }

        // Перевірка унікальності
        var exists = await _db.Users
            .AsNoTracking()
            .Where(u => u.Username == req.Username || u.Email == req.Email)
            .Select(u => new { u.Username, u.Email })
            .FirstOrDefaultAsync();

        if (exists != null)
        {
            if (exists.Username == req.Username)
                return BadRequest(new { error = "Username already exists" });
            if (exists.Email == req.Email)
                return BadRequest(new { error = "An account with this email already exists" });
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        var user = new User
        {
            Username = req.Username,
            Email = req.Email,
            Password = hash,
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return StatusCode(201, new { message = "Admin registered successfully", adminId = user.Id });
        }
        catch (DbUpdateException ex) when (ex.InnerException != null)
        {
            // Перехоплення унікальності
            var msg = ex.InnerException.Message;
            if (msg.Contains("users_username_key", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Username already exists" });
            if (msg.Contains("users_email_key", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "An account with this email already exists" });

            return StatusCode(500, new { error = "Error creating admin: " + ex.Message });
        }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> LoginAdmin([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required" });

        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email && u.Role == "admin");
        if (admin == null || !BCrypt.Net.BCrypt.Verify(req.Password, admin.Password))
            return BadRequest(new { error = "Invalid email or password" });

        var (token, exp) = _jwt.CreateToken(admin.Id, admin.Role, admin.Username);
        return new LoginResponse { Token = token};
    }

    // POST /api/auth/logout  (JWT статлес — просто відповідаємо)
    [HttpPost("logout")]
    public IActionResult LogoutAdmin() => Ok(new { message = "Logged out successfully" });

    // GET /api/auth/isAdmin
    [Authorize(Roles = "admin")]
    [HttpGet("isAdmin")]
    public IActionResult IsAdmin()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);

        return Ok(new { isAdmin });
    }

}
