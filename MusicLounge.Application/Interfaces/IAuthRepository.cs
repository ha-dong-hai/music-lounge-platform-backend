using System.Threading.Tasks;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Interfaces;

public interface IAuthRepository
{
    Task<bool> IsCitizenCardNumberExists(int userId, string citizenCardNumber);

    Task<User?> GetByEmail(string email);

    Task<User?> GetById(int userId);

    Task<User?> GetByIdAsNoTracking(int userId);

    Task Add(User user);

    Task<int> SaveChanges();
}
