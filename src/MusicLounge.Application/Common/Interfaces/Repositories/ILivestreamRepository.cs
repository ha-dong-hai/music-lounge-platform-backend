using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ILivestreamRepository : IRepository<Livestream, int>
{
    Task<Livestream?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);
    Task<Livestream?> GetByShowIdAsync(int showId, CancellationToken ct = default);
    Task<bool> HasViewerAccessAsync(int livestreamId, int userId, CancellationToken ct = default);
    Task<(IReadOnlyList<LivestreamChatMessage> Items, int TotalCount)> GetChatMessagesAsync(int livestreamId, int page, int pageSize, CancellationToken ct = default);
    void AddTicketDetail(LivestreamTicketDetail detail);
    Task<IReadOnlyList<Guid>> GetConfirmedLivestreamTicketIdsWithoutDetailAsync(int showId, CancellationToken ct = default);
}
