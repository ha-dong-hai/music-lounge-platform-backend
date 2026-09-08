using MusicLounge.Application.Common.Abstractions;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Lounges.DTOs;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Admin.Queries.GetVenueReviewQueue;

/// <param name="Status">
/// Mặc định Pending — trạng thái duy nhất có việc phải làm. Cho phép lọc sang Rejected để xem lại
/// các hồ sơ đã từ chối, vì một hồ sơ bị từ chối vẫn có thể được duyệt lại sau khi Owner sửa.
/// </param>
public sealed record GetVenueReviewQueueQuery(
    LoungeStatus Status = LoungeStatus.Pending,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<VenueReviewItemDto>>;
