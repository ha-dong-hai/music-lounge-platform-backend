using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Repositories;

internal sealed class TicketRepository : Repository<Ticket, Guid>, ITicketRepository
{
    private readonly ApplicationDbContext _ctx;

    public TicketRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    private IQueryable<Ticket> WithDetails()
        => _ctx.Tickets
            .AsNoTracking()
            .Include(t => t.Tier)
            .Include(t => t.Price)
            .Include(t => t.Show).ThenInclude(s => s.Lounge)
            .Include(t => t.PhysicalDetail)
            .Include(t => t.LivestreamDetail);

    public async Task<Ticket?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default)
        => await WithDetails()
            .FirstOrDefaultAsync(t => t.QrCode == qrCode, ct);

    public async Task<Ticket?> GetByIdWithDetailsAsync(Guid ticketId, CancellationToken ct = default)
        => await WithDetails()
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

    public async Task<Ticket?> GetByIdWithDetailsTrackedAsync(Guid ticketId, CancellationToken ct = default)
        => await _ctx.Tickets
            .Include(t => t.Tier)
            .Include(t => t.Price)
            .Include(t => t.Show)
            .Include(t => t.PhysicalDetail)
            .Include(t => t.LivestreamDetail)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

    public async Task<IReadOnlyList<Ticket>> GetIncomingTransfersAsync(
        int recipientUserId, CancellationToken ct = default)
    {
        // Unbounded in principle, but each row requires a distinct sender to have deliberately
        // initiated a transfer to this exact recipient — realistically single digits, not the
        // kind of "grows with total ticket volume" list that needs full page/pageSize plumbing.
        // A defensive cap is enough to rule out unbounded growth without changing the API contract.
        // SQLite khong ho tro ORDER BY tren cot DateTimeOffset - sap xep phia client sau khi lay ve.
        var tickets = await WithDetails()
            .Where(t => t.PendingTransferToUserId == recipientUserId)
            .Take(100)
            .ToListAsync(ct);
        return tickets.OrderByDescending(t => t.PendingTransferInitiatedAt).ToList();
    }

    public async Task<Ticket?> GetByQrCodeTrackedAsync(string qrCode, CancellationToken ct = default)
        => await _ctx.Tickets
            .Include(t => t.Tier)
            .Include(t => t.Price)
            .Include(t => t.Show).ThenInclude(s => s.Lounge)
            .Include(t => t.PhysicalDetail)
            .Include(t => t.LivestreamDetail)
            .FirstOrDefaultAsync(t => t.QrCode == qrCode, ct);

    public async Task<PaginatedResult<Ticket>> GetByBuyerAsync(
        int userId, int page, int pageSize, TicketStatus? status = null, CancellationToken ct = default)
    {
        var query = WithDetails()
            .Where(t => t.BuyerId == userId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        query = query.OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Ticket>(items, page, pageSize, total);
    }

    public async Task<PaginatedResult<Ticket>> GetByShowAsync(
        int showId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = WithDetails()
            .Include(t => t.Buyer)
            .Where(t => t.ShowId == showId)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Ticket>(items, page, pageSize, total);
    }

    public Task<int> CountConfirmedByPriceAsync(int priceId, CancellationToken ct = default)
        => _ctx.Tickets.CountAsync(
            t => t.PriceId == priceId &&
                 (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending), ct);

    public Task<int> CountActiveHoldsByPriceAsync(int priceId, CancellationToken ct = default)
        => _ctx.TicketHolds.CountAsync(
            h => h.PriceId == priceId && h.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public Task<int> CountConfirmedByShowAsync(int showId, CancellationToken ct = default)
        => _ctx.Tickets.CountAsync(
            t => t.ShowId == showId &&
                 (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending), ct);

    public void AddPhysicalDetail(PhysicalTicketDetail detail)
        => _ctx.PhysicalTicketDetails.Add(detail);

    public async Task<IReadOnlyDictionary<int, int>> GetReservedQuantitiesByPriceIdsAsync(
        IReadOnlyCollection<int> priceIds, CancellationToken ct = default)
    {
        var result = priceIds.ToDictionary(id => id, _ => 0);
        if (priceIds.Count == 0) return result;

        var ticketCounts = await _ctx.Tickets
            .Where(t => priceIds.Contains(t.PriceId) &&
                        (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Pending))
            .GroupBy(t => t.PriceId)
            .Select(g => new { PriceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Filtering ExpiresAt client-side to match the pattern already used by the quota
        // validators — combining the Contains(priceIds) predicate with a DateTimeOffset
        // inequality in one query does not translate under the SQLite provider used in tests.
        var activeHolds = await _ctx.TicketHolds
            .Where(h => priceIds.Contains(h.PriceId) && !h.IsReleased)
            .ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var holdSums = activeHolds
            .Where(h => h.ExpiresAt > now)
            .GroupBy(h => h.PriceId)
            .ToDictionary(g => g.Key, g => g.Sum(h => h.Quantity));

        foreach (var tc in ticketCounts)
            result[tc.PriceId] = tc.Count;
        foreach (var (priceId, heldQty) in holdSums)
            result[priceId] = result.GetValueOrDefault(priceId) + heldQty;

        return result;
    }

    public async Task<int> GetReservedQuantityByTierAsync(int tierId, CancellationToken ct = default)
    {
        var priceIds = await _ctx.TicketPrices
            .Where(p => p.TierId == tierId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (priceIds.Count == 0) return 0;

        var reserved = await GetReservedQuantitiesByPriceIdsAsync(priceIds, ct);
        return reserved.Values.Sum();
    }

    public async Task<int> GetReservedQuantityByShowAndAccessTypeAsync(
        int showId, AccessType accessType, CancellationToken ct = default)
    {
        var priceIds = await _ctx.TicketPrices
            .Where(p => p.Tier.LoungeShowId == showId && p.Tier.AccessType == accessType)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (priceIds.Count == 0) return 0;

        var reserved = await GetReservedQuantitiesByPriceIdsAsync(priceIds, ct);
        return reserved.Values.Sum();
    }

    public async Task<int> GetReservedQuantityByShowAsync(int showId, CancellationToken ct = default)
    {
        var priceIds = await _ctx.TicketPrices
            .Where(p => p.Tier.LoungeShowId == showId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (priceIds.Count == 0) return 0;

        var reserved = await GetReservedQuantitiesByPriceIdsAsync(priceIds, ct);
        return reserved.Values.Sum();
    }

    public async Task<int> GetReservedQuantityByZoneAsync(
        int showId, int zoneId, CancellationToken ct = default)
    {
        var priceIds = await _ctx.TicketPrices
            .Where(p => p.Tier.LoungeShowId == showId && p.Tier.ZoneId == zoneId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (priceIds.Count == 0) return 0;

        var reserved = await GetReservedQuantitiesByPriceIdsAsync(priceIds, ct);
        return reserved.Values.Sum();
    }

    public async Task<bool> TryInitiateTransferAsync(
        Guid ticketId, int recipientUserId, DateTimeOffset initiatedAt, CancellationToken ct = default)
    {
        var affected = await _ctx.Tickets
            .Where(t => t.Id == ticketId && t.PendingTransferToUserId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.PendingTransferToUserId, recipientUserId)
                .SetProperty(t => t.PendingTransferInitiatedAt, initiatedAt), ct);
        return affected > 0;
    }
}
