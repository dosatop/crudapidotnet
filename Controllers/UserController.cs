using CrudApi.Models;
using CrudApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrudApi.Controllers;


[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllUsers();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(User user)
    {
        var existingUser = await _userService.GetAllUsers();
        if (existingUser.Any(u => u.Email == user.Email))
        {
            return BadRequest("Email already exists");
        }

        var hashPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);
        user = new User
        {
            Name = user.Name,
            Email = user.Email,
            Password = hashPassword,
            Role = user.Role ?? "User",
        };
        var createdUser = await _userService.CreateUser(user);

        var response = new UserResponseDto
        {
            Id = createdUser.Id,
            Name = createdUser.Name,
            Email = createdUser.Email,
            Role = createdUser.Role ?? "User"
        };
        return CreatedAtAction(nameof(GetAllUsers), new { id = createdUser.Id }, response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        var user = await _userService.GetUserById(id);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [Authorize(Roles = "User, Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, User updatedUser)
    {
        var user = await _userService.Update(id, updatedUser);
        if (user == null)
        {
            return NotFound();
        }
        return NoContent();
    }

    [Authorize(Roles = "User, Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var success = await _userService.Delete(id);
        if (!success)
        {
            return NotFound();
        }
        return NoContent();
    }

}