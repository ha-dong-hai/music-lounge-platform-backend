using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.TicketTiers.Queries.GetTicketTiers;

internal sealed class GetTicketTiersQueryHandler
    : IRequestHandler<GetTicketTiersQuery, IReadOnlyList<TicketTierSummaryDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;

    public GetTicketTiersQueryHandler(IUnitOfWork uow, ITicketRepository ticketRepo)
    {
        _uow = uow;
        _ticketRepo = ticketRepo;
    }

    public async Task<IReadOnlyList<TicketTierSummaryDto>> Handle(
        GetTicketTiersQuery request, CancellationToken ct)
    {
        var tiers = await _uow.Repository<TicketTier, int>()
            .FindAsync(t => t.LoungeShowId == request.ShowId, ct);

        var tierIds = tiers.Select(t => t.Id).ToList();
        var prices = await _uow.Repository<TicketPrice, int>()
            .FindAsync(p => tierIds.Contains(p.TierId), ct);
        var pricesByTier = prices.ToLookup(p => p.TierId);

        // Live-computed từ tickets + holds thực tế (không dùng TicketPrice.Sold — cột đó không
        // được ghi ở bất kỳ đâu trong codebase, luôn = 0, nên luôn hiển thị sai "còn đủ vé").
        var reserved = await _ticketRepo.GetReservedQuantitiesByPriceIdsAsync(
            prices.Select(p => p.Id).ToList(), ct);

        return tiers.Select(t => new TicketTierSummaryDto(
            t.Id,
            t.Name,
            t.Description,
            t.AccessType,
            t.TotalCapacity,
            t.ZoneId,
            pricesByTier[t.Id].Select(p => new TicketPriceSummaryDto(
                p.Id,
                p.Name,
                p.Price,
                p.Quota,
                p.SaleStart,
                p.SaleEnd,
                p.PurchaseChannel,
                p.Quota.HasValue ? Math.Max(0, p.Quota.Value - reserved[p.Id]) : null))
            .ToList()))
        .ToList();
    }
}
