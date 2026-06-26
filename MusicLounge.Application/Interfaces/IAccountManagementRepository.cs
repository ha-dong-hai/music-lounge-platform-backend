using System.Linq;
using System.Threading.Tasks;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Interfaces;

public interface IAccountManagementRepository
{
    IQueryable<User> GetQueryable();

    Task<User?> GetById(int id);

    Task<bool> IsEmailExists(string email, int? ignoreId = null);

    Task Add(User user);

    Task<int> SaveChanges();
}
