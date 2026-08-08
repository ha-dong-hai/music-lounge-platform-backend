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
}
