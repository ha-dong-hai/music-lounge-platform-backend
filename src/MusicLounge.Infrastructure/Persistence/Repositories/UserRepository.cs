using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class UserRepository : Repository<User, int>, IUserRepository
{
    private readonly ApplicationDbContext _ctx;

    public UserRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public async Task<PaginatedResult<UserAdminDto>> SearchAsync(
        string? searchText, UserRole? role, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _ctx.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var s = searchText.Trim();
            query = query.Where(u =>
                u.Email.Contains(s) || u.FullName.Contains(s) || (u.Phone != null && u.Phone.Contains(s)));
        }

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserAdminDto(
                u.Id, u.Email, u.FullName, u.Phone, u.AvatarUrl,
                u.Role.ToString(), u.IsActive, u.EmailVerifiedAt != null, u.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<UserAdminDto>(items, page, pageSize, total);
    }
}
