using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILivestreamRepository : IRepository<Livestream, int>
{
    Task<Livestream?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<Livestream?> GetByShowIdAsync(int showId, CancellationToken ct = default);
    Task<bool> HasViewerAccessAsync(int livestreamId, int userId, CancellationToken ct = default);

    /// <summary>
    /// Ghi nhận một người xem vừa vào: tăng số đang xem, cộng một lượt xem, và nâng đỉnh nếu số
    /// hiện tại vượt đỉnh cũ. Trả về số người đang xem sau khi cộng.
    ///
    /// Nằm ở đây chứ không nằm trong LivestreamHub vì hub SignalR gần như không kiểm thử được, mà
    /// PeakViewerCount/TotalViews đúng là loại cột chỉ test mới phát hiện ra là chưa từng được ghi:
    /// trang thống kê chỉ lặng lẽ hiện 0.
    /// </summary>
    Task<int> RecordViewerJoinedAsync(int livestreamId, CancellationToken ct = default);

    /// <summary>Ghi nhận một người xem vừa rời. Không hạ đỉnh — đỉnh là số liệu cả buổi.</summary>
    Task<int> RecordViewerLeftAsync(int livestreamId, CancellationToken ct = default);
    // Giống HasViewerAccessAsync nhưng trả về chính Ticket (tracked, kèm LivestreamDetail) thay vì
    // chỉ bool — dùng khi cần ghi nhận phiên xem thật (TicketId cho LivestreamViewingSession, cập
    // nhật FirstAccessedAt/LastAccessedAt), không thay chữ ký HasViewerAccessAsync để không ảnh
    // hưởng GetChatHistoryQueryHandler/LivestreamHub đang dùng bản bool.
    Task<Ticket?> GetViewerTicketAsync(int livestreamId, int userId, CancellationToken ct = default);
    Task<(IReadOnlyList<LivestreamChatMessage> Items, int TotalCount)> GetChatMessagesAsync(int livestreamId, int page, int pageSize, CancellationToken ct = default);
    void AddTicketDetail(LivestreamTicketDetail detail);
    Task<IReadOnlyList<Guid>> GetConfirmedLivestreamTicketIdsWithoutDetailAsync(int showId, CancellationToken ct = default);
}
