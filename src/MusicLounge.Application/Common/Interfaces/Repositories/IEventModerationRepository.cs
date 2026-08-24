using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface IEventModerationRepository : IRepository<EventModeration, int>
{
    Task<PaginatedResult<EventModerationDto>> GetPendingAsync(
        ModerationTargetType? targetType, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Show đang Pending kèm tín hiệu AI moderation (score/risk/flag) — dùng cho danh sách
    /// Admin duyệt event, khác GetPendingAsync ở chỗ có sẵn tên show/phòng trà/ngày diễn thay vì
    /// chỉ TargetId thô.</summary>
    Task<PaginatedResult<PendingLoungeShowDto>> GetPendingShowsAsync(
        int page, int pageSize, CancellationToken ct = default);

    Task<EventModeration?> GetByTargetAsync(
        ModerationTargetType targetType, int targetId, CancellationToken ct = default);
}
