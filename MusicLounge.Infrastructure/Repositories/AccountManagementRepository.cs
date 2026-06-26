using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

public class AccountManagementRepository : IAccountManagementRepository
{
    private readonly AppDbContext _context;

    public AccountManagementRepository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<User> GetQueryable()
    {
        return _context.Users.AsQueryable();
    }

    public async Task<User?> GetById(int id)
    {
        return await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<bool> IsEmailExists(string email, int? ignoreId = null)
    {
        return await _context.Users.AnyAsync(x => x.Email == email && (!ignoreId.HasValue || x.Id != ignoreId.Value));
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
