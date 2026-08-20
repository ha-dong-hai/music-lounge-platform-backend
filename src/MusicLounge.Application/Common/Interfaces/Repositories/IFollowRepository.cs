using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IFollowRepository : IRepository<Follow, int>
{
    Task<PaginatedResult<FollowedLoungeDto>> GetFollowedLoungesByUserAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetFollowerUserIdsAsync(int loungeId, CancellationToken ct = default);
}
