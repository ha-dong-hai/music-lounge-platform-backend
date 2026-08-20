using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class LivestreamRepository : Repository<Livestream, int>, ILivestreamRepository
{
    private readonly ApplicationDbContext _db;

    public LivestreamRepository(ApplicationDbContext db) : base(db) => _db = db;

    public async Task<Livestream?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => await _db.Livestreams
            .AsNoTracking()
            .Include(l => l.LoungeShow)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<Livestream?> GetByShowIdAsync(int showId, CancellationToken ct = default)
        => await _db.Livestreams
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoungeShowId == showId, ct);

    public async Task<bool> HasViewerAccessAsync(int livestreamId, int userId, CancellationToken ct = default)
        => await _db.Livestreams
            .Where(l => l.Id == livestreamId)
            .SelectMany(l => l.LoungeShow.Tickets)
            .AnyAsync(t =>
                t.BuyerId == userId &&
                t.Status == TicketStatus.Confirmed &&
                t.Tier.AccessType == AccessType.Livestream,
                ct);

    public async Task<(IReadOnlyList<LivestreamChatMessage> Items, int TotalCount)> GetChatMessagesAsync(
        int livestreamId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.LivestreamChatMessages
            .AsNoTracking()
            .Where(m => m.LivestreamId == livestreamId)
            .Include(m => m.User);

        var totalCount = await query.CountAsync(ct);
        // OrderBy(DateTimeOffset) doesn't translate under the SQLite provider used in tests (same
        // class of issue documented elsewhere in this codebase) — a real 500 caught only once this
        // endpoint actually got test coverage. Order by Id instead of SentAt: chat messages are
        // never backdated, so insertion order (Id) and SentAt order are always identical here,
        // and Id (int) keeps pagination server-side on both SQLite and SQL Server — unlike the
        // donation list above, chat history can grow unbounded over a livestream's lifetime, so
        // materializing the whole thing into memory to sort client-side isn't an acceptable trade.
        var items = await query
            .OrderByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void AddTicketDetail(LivestreamTicketDetail detail)
        => _db.LivestreamTicketDetails.Add(detail);

    public async Task<IReadOnlyList<Guid>> GetConfirmedLivestreamTicketIdsWithoutDetailAsync(
        int showId, CancellationToken ct = default)
        => await _db.Tickets
            .Where(t => t.ShowId == showId
                && t.Status == TicketStatus.Confirmed
                && t.Tier.AccessType == AccessType.Livestream
                && !_db.LivestreamTicketDetails.Any(d => d.TicketId == t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);
}
