using CrudApi.Models;
using CrudApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CrudApi.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }
    public async Task<List<User>> GetAllUsers()
    {
        return await _context.Users.ToListAsync();
    }
    public async Task<User> CreateUser(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> GetUserById(string id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> Update(string id, User updatedUser)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return null;

        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;

        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> Delete(string id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

}