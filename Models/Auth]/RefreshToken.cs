public class RefreshToken
{
    public int Id { get; set; }
    public required string Token { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsRevoked => RevokedAt != null;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;
    public required int UserId { get; set; }
    public required string CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; } // OK for now
    public string? Device { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceName { get; set; }
    public string? Browser { get; set; }
    public string? Os { get; set; }
    public DateTime? LastActiveAt { get; set; }
}