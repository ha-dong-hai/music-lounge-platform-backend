using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Queries.GetShowTicketStats;

internal sealed class GetShowTicketStatsQueryHandler
    : IRequestHandler<GetShowTicketStatsQuery, ShowTicketStatsDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetShowTicketStatsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<ShowTicketStatsDto> Handle(GetShowTicketStatsQuery request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<Domain.Entities.MusicLounge, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.MusicLounge), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xem thống kê vé của show này.");

        // Đếm trực tiếp trên bảng Ticket (không dùng TicketPrice.Sold — field đó không bao giờ
        // được ghi, xem comment trên chính entity) để số liệu luôn khớp thời gian thực. "Đã bán"
        // gồm Confirmed và Used (đã check-in) — Pending (đang giữ chỗ chưa thanh toán)/Cancelled/
        // Refunded không tính, khớp quy ước đã dùng ở GetLoungeShowDetailQueryHandler/RateShowCommandHandler.
        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => t.ShowId == request.ShowId
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);

        var priceIds = tickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id);

        var tierIds = prices.Select(p => p.TierId).Distinct().ToList();
        var tiers = await _uow.Repository<TicketTier, int>().FindAsync(t => tierIds.Contains(t.Id), ct);
        var tierById = tiers.ToDictionary(t => t.Id);

        var byPrice = tickets
            .GroupBy(t => t.PriceId)
            .Select(g =>
            {
                var price = priceById[g.Key];
                var tier = tierById[price.TierId];
                var quantitySold = g.Count();
                return new TicketPriceStatDto(
                    tier.Id,
                    tier.Name,
                    price.Id,
                    price.Name,
                    price.Price,
                    quantitySold,
                    quantitySold * price.Price,
                    g.Count(t => t.Status == TicketStatus.Used));
            })
            .OrderBy(x => x.TierName)
            .ThenBy(x => x.PriceName)
            .ToList();

        return new ShowTicketStatsDto(
            show.Id,
            show.Name,
            tickets.Count,
            tickets.Sum(t => priceById[t.PriceId].Price),
            tickets.Count(t => t.Status == TicketStatus.Used),
            byPrice);
    }
}
