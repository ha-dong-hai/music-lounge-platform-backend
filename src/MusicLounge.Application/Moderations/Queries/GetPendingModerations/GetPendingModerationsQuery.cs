using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Moderations.DTOs;

namespace MusicLounge.Application.Moderations.Queries.GetPendingModerations;

public sealed record GetPendingModerationsQuery(
    string? TargetType = null,
    int Page = 1,
    int PageSize = 20,
    // MLACP-504: lấy bản chờ duyệt của ĐÚNG một đối tượng (vd buổi diễn đang mở). Bắt buộc đi cùng TargetType hợp lệ —
    // xem validator.
    Guid? TargetId = null
) : IQuery<PaginatedResult<EventModerationDto>>;
