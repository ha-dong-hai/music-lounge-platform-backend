using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IUserRepository : IRepository<User, int>
{
    Task<PaginatedResult<UserAdminDto>> SearchAsync(
        string? searchText, UserRole? role, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);
}
