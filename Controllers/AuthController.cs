using CrudApi.Data;
using CrudApi.Extensions;
using CrudApi.Models.Auth;
using CrudApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CrudApi.Controllers;

[EnableRateLimiting("globalPolicy")]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtServices _jwtServices;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly AppDbContext _context;
    public AuthController(JwtServices jwtServices, RefreshTokenService refreshTokenService, AppDbContext context)
    {
        _jwtServices = jwtServices;
        _refreshTokenService = refreshTokenService;
        _context = context;
    }

    [EnableRateLimiting("loginPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        var user = _context.Users.FirstOrDefault(u => u.Email == loginDto.Email);
        if (user == null)
        {
            return Unauthorized("Invalid email or password");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remainingLockout = user.LockoutEnd.Value - DateTime.UtcNow;
            return Unauthorized($"Account locked. Try again in {remainingLockout.Minutes} minutes and {remainingLockout.Seconds} seconds.");
        }

        var isValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password);

        if (!isValid)
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                await _context.SaveChangesAsync();
                return Unauthorized("Account locked due to multiple failed login attempts. Try again in 15 minutes.");
            }

            await _context.SaveChangesAsync();
            return Unauthorized("Invalid email or password");
        }
        // ✅ SUCCESS → RESET LOCKOUT
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        var accessToken = _jwtServices.GenerateToken(user.Id.ToString(), user.Email, user.Role ?? "User");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        var deviceName = "Unknown Device";

        if (userAgent.Contains("Windows"))
            deviceName = "Windows PC";
        else if (userAgent.Contains("Mac"))
            deviceName = "Mac";
        else if (userAgent.Contains("iPhone"))
            deviceName = "iPhone";
        else if (userAgent.Contains("Android"))
            deviceName = "Android";

        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            user.Id,
            ipAddress,
            userAgent,
            deviceName
        );

        return Ok(new { AccessToken = accessToken, RefreshToken = refreshToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _refreshTokenService.RefreshTokenAsync(dto.RefreshToken, ipAddress ?? "Unknown");

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task Logout(string refreshToken)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.Token == _refreshTokenService.HashToken(refreshToken));

        if (token == null) return;

        token.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    [HttpGet("tokens")]
    public async Task<IActionResult> GetTokens()
    {
        var tokens = await _context.RefreshTokens.ToListAsync();
        return Ok(tokens);
    }

    [HttpGet("sessions")]
    // [Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var userId = User.GetUserId();

        var sessions = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .Select(rt => new
            {
                rt.Id,
                rt.CreatedAt,
                rt.ExpiresAt,
                rt.CreatedByIp,
                rt.RevokedByIp,
                rt.UserAgent
            })
            .ToListAsync();

        return Ok(sessions);
    }

    [HttpDelete("sessions/{id}")]
    // [Authorize]
    public async Task<IActionResult> RevokeSession(int id)
    {
        var userId = User.GetUserId();

        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Id == id && rt.UserId == userId);

        if (token == null) return NotFound();

        token.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok("Sessions revoked successfully");
    }

    [Authorize]
    [HttpGet("sessions/current")]
    public async Task<IActionResult> GetCurrentSession()
    {
        var userId = User.GetUserId();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var session = await _context.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                x.CreatedByIp == ip &&
                x.UserAgent == userAgent &&
                x.RevokedAt == null &&
                x.ExpiresAt > DateTime.UtcNow
            )
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound();

        return Ok(session);
    }

    [HttpPost("logout-all")]
    // [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = int.Parse(User.FindFirst("id")!.Value);

        var userIdClaim = User.FindFirst("id");

        if (userIdClaim == null)
            return Unauthorized("User ID not found in token");


        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok("All sessions revoked successfully");
    }


    [HttpPost("logout-others")]
    [Authorize]
    public async Task<IActionResult> LogoutOtherSessions()
    {
        var userId = User.GetUserId();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

        var sessions = await _context.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                x.RevokedAt == null &&
                x.ExpiresAt > DateTime.UtcNow
            )
            .ToListAsync();

        var currentSession = sessions.FirstOrDefault(x =>
            x.CreatedByIp == ip &&
            x.UserAgent == userAgent
        );

        foreach (var session in sessions)
        {
            if (session != currentSession)
            {
                session.RevokedAt = DateTime.UtcNow;
                session.RevokedByIp = ip;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Other sessions revoked" });
    }

}