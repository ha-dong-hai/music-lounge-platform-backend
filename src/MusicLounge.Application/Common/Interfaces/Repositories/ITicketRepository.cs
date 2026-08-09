using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces.Repositories;

public interface ITicketRepository : IRepository<Ticket, Guid>
{
    Task<Ticket?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default);
    Task<Ticket?> GetByQrCodeTrackedAsync(string qrCode, CancellationToken ct = default);
    Task<Ticket?> GetByIdWithDetailsAsync(Guid ticketId, CancellationToken ct = default);
    Task<Ticket?> GetByIdWithDetailsTrackedAsync(Guid ticketId, CancellationToken ct = default);
    Task<PaginatedResult<Ticket>> GetByBuyerAsync(int userId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<Ticket>> GetByShowAsync(int showId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> GetIncomingTransfersAsync(int recipientUserId, CancellationToken ct = default);
    Task<int> CountConfirmedByPriceAsync(int priceId, CancellationToken ct = default);
    Task<int> CountActiveHoldsByPriceAsync(int priceId, CancellationToken ct = default);
    Task<int> CountConfirmedByShowAsync(int showId, CancellationToken ct = default);
    void AddPhysicalDetail(PhysicalTicketDetail detail);

    /// <summary>
    /// Single source of truth for "how many seats/tickets are already spoken for" at the price
    /// level: confirmed + pending tickets, plus quantity held by active (non-expired, non-released)
    /// holds. Used both to VALIDATE a new hold/sale (write path) and to DISPLAY remaining
    /// availability (read path) — one formula, so the two can never drift apart. Batched by design:
    /// callers needing a single price still pass a one-element list, so there is exactly one
    /// implementation of this query for both single-price and whole-show use.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetReservedQuantitiesByPriceIdsAsync(
        IReadOnlyCollection<int> priceIds, CancellationToken ct = default);

    /// <summary>Sum of <see cref="GetReservedQuantitiesByPriceIdsAsync"/> across every price under a tier.</summary>
    Task<int> GetReservedQuantityByTierAsync(int tierId, CancellationToken ct = default);

    /// <summary>Sum of <see cref="GetReservedQuantitiesByPriceIdsAsync"/> across every price of the given access type under a show.</summary>
    Task<int> GetReservedQuantityByShowAndAccessTypeAsync(int showId, AccessType accessType, CancellationToken ct = default);

    /// <summary>Sum of <see cref="GetReservedQuantitiesByPriceIdsAsync"/> across every price under a show, regardless of tier/access type — used for the whole-show subscription ticket cap (D14), which applies across physical and online tiers combined.</summary>
    Task<int> GetReservedQuantityByShowAsync(int showId, CancellationToken ct = default);

    /// <summary>
    /// Sum of <see cref="GetReservedQuantitiesByPriceIdsAsync"/> across every price of every tier
    /// that shares the given physical seating zone WITHIN one show — §6.11 row 4
    /// (SUM(price.quota) ≤ seating_areas.capacity): a zone's real physical capacity is the true cap
    /// regardless of how many separate ticket tiers (VIP, Early-bird, Walk-in, ...) were carved out
    /// of it, and per-tier TotalCapacity alone cannot catch two tiers on the same zone jointly
    /// overselling it. Scoped to one show (not the zone's lifetime across every show ever held
    /// there) because a venue-level zone is reused night to night — SeatingZone.
    /// </summary>
    Task<int> GetReservedQuantityByZoneAsync(int showId, int zoneId, CancellationToken ct = default);

    /// <summary>
    /// Atomically sets the pending-transfer fields only if no transfer is already pending on this
    /// ticket. Closes a lost-update race: two near-simultaneous InitiateTicketTransfer calls for
    /// the same ticket can both pass an earlier in-memory "is one already pending?" check before
    /// either commits a normal load-then-save; the second silently overwrites the first with no
    /// error. Returns false (no row matched the guard) if a transfer was already pending by the
    /// time this executed — caller should surface that as a conflict, not swallow it.
    /// </summary>
    Task<bool> TryInitiateTransferAsync(
        Guid ticketId, int recipientUserId, DateTimeOffset initiatedAt, CancellationToken ct = default);
}
