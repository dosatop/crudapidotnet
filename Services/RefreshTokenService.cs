using System.Security.Cryptography;
using System.Text;
using CrudApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CrudApi.Services;

public class RefreshTokenService
{
    private readonly AppDbContext _context;
    private readonly JwtServices _jwtServices;

    public RefreshTokenService(AppDbContext context, JwtServices jwtServices)
    {
        _context = context;
        _jwtServices = jwtServices;
    }

    // PURE GENERATOR (NO DB SIDE EFFECTS)
    public Task<string> GenerateRefreshTokenAsync()
    {
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hashedToken = HashToken(refreshToken);
        return Task.FromResult(hashedToken);
    }

    public string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public async Task<string> CreateRefreshTokenAsync(int userId, string ipAddress, string? userAgent = null, string? deviceName = null)
    {
        var refreshToken = await GenerateRefreshTokenAsync();

        var entity = new RefreshToken
        {
            Token = refreshToken,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress,
            UserAgent = userAgent,
            DeviceName = deviceName,
            LastActiveAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    // MAIN REFRESH FLOW
    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == refreshToken);

            if (storedToken == null)
                throw new Exception("Invalid refresh token");

            // 🔴 REUSE DETECTION
            if (storedToken.RevokedAt != null)
            {
                await RevokeAllUserTokens(storedToken.UserId);
                await _context.SaveChangesAsync();
                throw new Exception("Token reuse detected");
            }

            // 🔴 EXPIRY CHECK
            if (storedToken.ExpiresAt <= DateTime.UtcNow)
                throw new Exception("Expired refresh token");

            var user = await _context.Users.FindAsync(storedToken.UserId);

            if (user == null)
                throw new Exception("User not found");

            // 🔑 GENERATE TOKENS
            var newRefreshToken = await GenerateRefreshTokenAsync();

            var newAccessToken = _jwtServices.GenerateToken(
                user.Id.ToString(),
                user.Email,
                user.Role ?? "User"
            );

            // 🔴 REVOKE OLD TOKEN
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = ipAddress;
            storedToken.ReplacedByToken = newRefreshToken;

            // 🔵 CREATE NEW TOKEN
            var newTokenEntity = new RefreshToken
            {
                Token = newRefreshToken,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedByIp = ipAddress
            };

            _context.RefreshTokens.Add(newTokenEntity);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // REVOKE ALL TOKENS FOR USER
    private async Task RevokeAllUserTokens(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }
    }
}