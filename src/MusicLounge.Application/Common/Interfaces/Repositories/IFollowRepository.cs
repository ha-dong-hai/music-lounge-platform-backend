using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Follows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IFollowRepository : IRepository<Follow, Guid>
{
    Task<PaginatedResult<FollowedLoungeDto>> GetFollowedLoungesByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetFollowerUserIdsAsync(Guid loungeId, CancellationToken ct = default);

    /// <summary>MLACP-503. Trong các phòng trà được hỏi, những phòng mà người dùng đang theo dõi.</summary>
    Task<IReadOnlySet<Guid>> GetFollowedAmongAsync(Guid userId, IReadOnlyCollection<Guid> loungeIds, CancellationToken ct = default);
}
