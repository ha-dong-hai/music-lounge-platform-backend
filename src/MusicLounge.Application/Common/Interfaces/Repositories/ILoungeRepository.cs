using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILoungeRepository
{
    Task<PaginatedResult<LoungeListItemDto>> GetAllAsync(
        string? city, int? ownerId, int page, int pageSize, CancellationToken ct = default);

    Task<LoungeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> IsFollowingAsync(int loungeId, int userId, CancellationToken ct = default);
}
