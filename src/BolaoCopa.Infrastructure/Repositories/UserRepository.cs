using BolaoCopa.Application.Interfaces;
using BolaoCopa.Domain.Entities;
using BolaoCopa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(Guid id) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public Task<User?> GetByEmailAsync(string email) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

    public Task<bool> ExistsByEmailAsync(string email) =>
        _context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant());

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }
}
