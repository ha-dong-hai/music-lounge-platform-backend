using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Admin.Queries.GetAdminWorkQueue;

/// <summary>MLACP-617 — số việc đang chờ Admin ở mỗi hàng đợi, kèm số đã quá hạn và hạn gần nhất.</summary>
public sealed record GetAdminWorkQueueQuery : IQuery<IReadOnlyList<AdminWorkQueueItemDto>>;

/// <param name="Key">Mã hàng đợi, khớp với mục menu của trang quản trị.</param>
/// <param name="Count">Số việc đang chờ — lấy từ CHÍNH truy vấn danh sách của trang đó, nên luôn bằng số dòng Admin thấy
/// khi bấm vào.</param>
/// <param name="OverdueCount">Số việc đã quá thời hạn cam kết. <c>null</c> = hàng đợi này chưa có thời hạn cam kết nào
/// (khác với 0 = có thời hạn và chưa việc nào quá).</param>
/// <param name="NextDueAt">Hạn gần nhất của một việc CHƯA quá hạn. <c>null</c> khi không có.</param>
public sealed record AdminWorkQueueItemDto(string Key, int Count, int? OverdueCount, DateTimeOffset? NextDueAt);
