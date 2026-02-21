using Microsoft.EntityFrameworkCore;
using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PiiGatewayDbContext _context;

    public UserRepository(PiiGatewayDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}
