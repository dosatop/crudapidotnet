// namespace CrudApi.Models;

// public class User
// {
//     public int Id { get; set; }
//     public required string Name { get; set; }
//     public required string Email { get; set; }

//     public string? Role { get; set; }
// }

using System.ComponentModel.DataAnnotations;

namespace CrudApi.Models;

public class User
{
    [Key]
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public string? Role { get; set; } = "User";
    public string? RefreshToken { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEnd { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
}