using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IFollowRepository : IRepository<Follow, int>
{
    Task<PaginatedResult<FollowedLoungeDto>> GetFollowedLoungesByUserAsync(
        int userId, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetFollowerUserIdsAsync(int loungeId, CancellationToken ct = default);

    /// <summary>MLACP-503. Trong các phòng trà được hỏi, những phòng mà người dùng đang theo dõi.</summary>
    Task<IReadOnlySet<int>> GetFollowedAmongAsync(int userId, IReadOnlyCollection<int> loungeIds, CancellationToken ct = default);
}
