using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Application.Common;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.TicketTiers.Queries.GetTicketTiers;

internal sealed class GetTicketTiersQueryHandler
    : IRequestHandler<GetTicketTiersQuery, IReadOnlyList<TicketTierSummaryDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ITicketRepository _ticketRepo;
    private readonly ISystemConfigService _config;

    public GetTicketTiersQueryHandler(
        IUnitOfWork uow, ITicketRepository ticketRepo, ISystemConfigService config)
    {
        _uow = uow;
        _ticketRepo = ticketRepo;
        _config = config;
    }

    /// <summary>BR-31: mốc Owner đặt, nhưng không bao giờ muộn hơn giờ nhận khách cuối.</summary>
    private static DateTimeOffset EffectiveEnd(TicketPrice price, DateTimeOffset lastEntry)
        => price.SaleEnd is { } explicitEnd && explicitEnd < lastEntry ? explicitEnd : lastEntry;

    public async Task<IReadOnlyList<TicketTierSummaryDto>> Handle(
        GetTicketTiersQuery request, CancellationToken ct)
    {
        // BR-31: can chinh buoi dien de biet moc dong ban mac dinh cua cac dot khong dat moc rieng.
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);
        var lastEntryMinutes = await _config.GetIntAsync(
            ConfigKeys.TicketLastEntryMinutes, TicketSaleWindow.DefaultLastEntryMinutes, ct);
        var lastEntry = TicketSaleWindow.LastEntry(show, lastEntryMinutes);

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
                EffectiveEnd(p, lastEntry),
                p.PurchaseChannel,
                p.Quota.HasValue ? Math.Max(0, p.Quota.Value - reserved[p.Id]) : null,
                p.SaleEnd != EffectiveEnd(p, lastEntry)))
            .ToList()))
        .ToList();
    }
}
