using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Analytics.Queries.GetTicketSalesTrend;

internal sealed class GetTicketSalesTrendQueryHandler
    : IRequestHandler<GetTicketSalesTrendQuery, TicketSalesTrendDto>
{
    // Same VN-local (UTC+7) convention as the other Analytics handlers — a ticket bought just after
    // midnight VN time is still stored as the previous UTC calendar day.
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetTicketSalesTrendQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<TicketSalesTrendDto> Handle(GetTicketSalesTrendQuery request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<Domain.Entities.MusicLounge, int>()
            .GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.MusicLounge), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem thống kê bán vé của sự kiện này.");

        // "Đã bán" — Confirmed hoặc đã Used (check-in rồi vẫn tính là đã bán); Pending/Cancelled/
        // Refunded không tính vào doanh số thực.
        var tickets = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => t.ShowId == request.ShowId
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);

        var tiers = await _uow.Repository<TicketTier, int>()
            .FindAsync(t => t.LoungeShowId == request.ShowId, ct);
        var tierById = tiers.ToDictionary(t => t.Id);

        var priceIds = tickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id);
        decimal TicketAmount(Ticket t) => priceById.TryGetValue(t.PriceId, out var p) ? p.Price : 0m;

        DateOnly VnDate(DateTimeOffset d) => DateOnly.FromDateTime(d.ToOffset(VnOffset).Date);

        var dailySales = tickets
            .GroupBy(t => VnDate(t.CreatedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailyTicketSalesDto(g.Key, g.Count(), g.Sum(TicketAmount)))
            .ToList();

        var byTier = tierById.Values
            .Select(tier =>
            {
                var tierTickets = tickets.Where(t => t.TierId == tier.Id).ToList();
                var sold = tierTickets.Count;
                var rate = tier.TotalCapacity is > 0
                    ? Math.Round((decimal)sold / tier.TotalCapacity.Value, 4)
                    : (decimal?)null;
                return new TicketTierSalesDto(tier.Id, tier.Name, sold, tier.TotalCapacity, rate);
            })
            .OrderByDescending(t => t.TicketsSold)
            .ToList();

        return new TicketSalesTrendDto(
            ShowId: show.Id,
            ShowName: show.Name,
            TotalTicketsSold: tickets.Count,
            TotalRevenue: tickets.Sum(TicketAmount),
            DailySales: dailySales,
            ByTier: byTier);
    }
}
