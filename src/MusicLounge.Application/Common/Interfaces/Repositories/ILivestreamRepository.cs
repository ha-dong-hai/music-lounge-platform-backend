using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILivestreamRepository : IRepository<Livestream, int>
{
    Task<Livestream?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<Livestream?> GetByShowIdAsync(int showId, CancellationToken ct = default);
    Task<bool> HasViewerAccessAsync(int livestreamId, int userId, CancellationToken ct = default);
    // Giống HasViewerAccessAsync nhưng trả về chính Ticket (tracked, kèm LivestreamDetail) thay vì
    // chỉ bool — dùng khi cần ghi nhận phiên xem thật (TicketId cho LivestreamViewingSession, cập
    // nhật FirstAccessedAt/LastAccessedAt), không thay chữ ký HasViewerAccessAsync để không ảnh
    // hưởng GetChatHistoryQueryHandler/LivestreamHub đang dùng bản bool.
    Task<Ticket?> GetViewerTicketAsync(int livestreamId, int userId, CancellationToken ct = default);
    Task<(IReadOnlyList<LivestreamChatMessage> Items, int TotalCount)> GetChatMessagesAsync(int livestreamId, int page, int pageSize, CancellationToken ct = default);
    void AddTicketDetail(LivestreamTicketDetail detail);
    Task<IReadOnlyList<Guid>> GetConfirmedLivestreamTicketIdsWithoutDetailAsync(int showId, CancellationToken ct = default);
}
