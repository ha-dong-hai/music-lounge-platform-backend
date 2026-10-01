using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILoungeRepository
{
    /// <param name="includeUnapproved">
    /// Chỉ bật khi người gọi đang xem phòng trà của chính mình. Với người ngoài, phòng trà chưa
    /// được duyệt không tồn tại (BR-01).
    /// </param>
    Task<PaginatedResult<LoungeListItemDto>> GetAllAsync(
        string? city, int? ownerId, bool includeUnapproved, int page, int pageSize,
        string? keyword = null, CancellationToken ct = default, string? provinceCode = null);

    Task<LoungeDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Hàng đợi hồ sơ phòng trà chờ Admin duyệt, cũ nhất trước. <paramref name="status"/> null = mọi trạng
    /// thái; <paramref name="keyword"/> (đã chuẩn hoá) khớp tên phòng trà, tên chủ hoặc địa chỉ (MLACP-502).</summary>
    Task<PaginatedResult<VenueReviewItemDto>> GetReviewQueueAsync(
        LoungeStatus? status, string? keyword, int page, int pageSize, CancellationToken ct = default);

    Task<bool> IsFollowingAsync(int loungeId, int userId, CancellationToken ct = default);
}
