using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsCitizenCardNumberExists(int userId, string citizenCardNumber)
    {
        return await _context.Users.AnyAsync(x => x.Id != userId && x.CitizenCardNumber == citizenCardNumber);
    }

    public async Task<User?> GetByEmail(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<User?> GetById(int userId)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);
    }

    public async Task<User?> GetByIdAsNoTracking(int userId)
    {
        return await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
    }

    public async Task Add(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task<int> SaveChanges()
    {
        return await _context.SaveChangesAsync();
    }
}
