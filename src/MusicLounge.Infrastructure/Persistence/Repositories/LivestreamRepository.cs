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

    // MLACP-140: lan xem dau tien chuyen ve sang TicketStatus.Used (de RateShowCommandHandler cho
    // phep danh gia sau show) — Used la trang thai KET THUC HOP LE cua 1 lan xem that, khong phai
    // trang thai loai bo. Chi nhan Confirmed o day se khoa vinh vien HlsUrl/chat/hub ngay sau lan
    // xem dau tien (kha nang chinh cua tinh nang gioi han phien dong thoi cung phu thuoc dieu nay —
    // khong co no thiet bi thu 2 khong bao gio qua duoc check nay du van con han muc).
    public async Task<bool> HasViewerAccessAsync(int livestreamId, int userId, CancellationToken ct = default)
        => await _db.Livestreams
            .Where(l => l.Id == livestreamId)
            .SelectMany(l => l.LoungeShow.Tickets)
            .AnyAsync(t =>
                t.BuyerId == userId &&
                (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used) &&
                t.Tier.AccessType == AccessType.Livestream,
                ct);

    public async Task<Ticket?> GetViewerTicketAsync(int livestreamId, int userId, CancellationToken ct = default)
    {
        var livestream = await _db.Livestreams.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == livestreamId, ct);
        if (livestream is null) return null;

        // Tracked (khong AsNoTracking) — handler goi ham nay se sua LivestreamDetail va luu qua
        // cung 1 SaveChangesAsync, khong can goi Update() rieng. Load LivestreamDetail RIENG qua
        // Entry().Reference() thay vi .Include(t => t.LivestreamDetail) trong cung query —
        // confirmed thuc nghiem: ket hop Include tren 1 nav voi Where loc qua nav khac
        // (t.Tier.AccessType) khien FirstOrDefaultAsync tra ve null tren SQLite test provider
        // (khong loi, chi sai ket qua) du du lieu khop day du dieu kien.
        // Confirmed HOAC Used — cung ly do voi HasViewerAccessAsync o tren.
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t =>
                t.ShowId == livestream.LoungeShowId &&
                t.BuyerId == userId &&
                (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used) &&
                t.Tier.AccessType == AccessType.Livestream,
                ct);
        if (ticket is not null)
            await _db.Entry(ticket).Reference(t => t.LivestreamDetail).LoadAsync(ct);
        return ticket;
    }

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
