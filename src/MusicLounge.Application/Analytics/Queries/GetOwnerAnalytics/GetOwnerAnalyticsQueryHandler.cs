using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerAnalytics;

internal sealed class GetOwnerAnalyticsQueryHandler
    : IRequestHandler<GetOwnerAnalyticsQuery, OwnerAnalyticsDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetOwnerAnalyticsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<OwnerAnalyticsDto> Handle(GetOwnerAnalyticsQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem thống kê của venue này.");

        var shows = await _uow.Repository<LoungeShow, int>()
            .FindAsync(s => s.LoungeId == request.LoungeId, ct);
        var showIds = shows.Select(s => s.Id).ToHashSet();
        var now = DateTimeOffset.UtcNow;

        var tickets = await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => showIds.Contains(t.ShowId) && t.Status == TicketStatus.Confirmed, ct);

        var priceIds = tickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await _uow.Repository<TicketPrice, int>()
            .FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id);

        var tiers = await _uow.Repository<TicketTier, int>()
            .FindAsync(t => showIds.Contains(t.LoungeShowId), ct);
        var tierById = tiers.ToDictionary(t => t.Id);
        var tiersByShow = tiers.ToLookup(t => t.LoungeShowId);

        decimal TicketAmount(Ticket t) => priceById.TryGetValue(t.PriceId, out var p) ? p.Price : 0m;
        bool IsOffline(Ticket t) => tierById.TryGetValue(t.TierId, out var tier) && tier.AccessType == AccessType.Physical;

        var ticketsByShow = tickets.ToLookup(t => t.ShowId);

        var ratings = await _uow.Repository<LoungeShowRating, int>()
            .FindAsync(r => showIds.Contains(r.LoungeShowId) && !r.IsRemoved, ct);
        var ratingsByShow = ratings.ToLookup(r => r.LoungeShowId);

        var fnbOrders = await _uow.Repository<FnbOrder, int>()
            .FindAsync(o => o.LoungeId == request.LoungeId && o.Status == FnbOrderStatus.Paid, ct);

        var performances = await _uow.Repository<Performance, int>()
            .FindAsync(p => showIds.Contains(p.LoungeShowId), ct);
        var performerIds = performances.Select(p => p.PerformerId).Distinct().ToList();
        var performers = await _uow.Repository<Performer, int>().FindAsync(p => performerIds.Contains(p.Id), ct);
        var performerById = performers.ToDictionary(p => p.Id);
        var mainPerformerByShow = performances
            .Where(p => p.Role == PerformerRole.Main)
            .GroupBy(p => p.LoungeShowId)
            .ToDictionary(g => g.Key, g => performerById.TryGetValue(g.First().PerformerId, out var pf) ? pf.Name : null);

        var performanceIds = performances.Select(p => p.Id).ToHashSet();
        var pendingPayoutDonations = (await _uow.Repository<Donation, int>()
            .FindAsync(d => performanceIds.Contains(d.PerformanceId) && d.Status == DonationStatus.OwnerReceived, ct))
            .ToList();

        var topShows = showIds
            .Select(id =>
            {
                var showTickets = ticketsByShow[id].ToList();
                var revenue = showTickets.Sum(TicketAmount);
                var showRatings = ratingsByShow[id].ToList();
                var totalCapacity = tiersByShow[id].Sum(t => t.TotalCapacity ?? 0);
                var show = shows.First(s => s.Id == id);
                return new TopShowDto(
                    id,
                    show.Name,
                    show.ScheduledStart,
                    mainPerformerByShow.TryGetValue(id, out var name) ? name : null,
                    showTickets.Count,
                    totalCapacity > 0 ? totalCapacity : null,
                    showRatings.Count > 0 ? (decimal?)Math.Round(showRatings.Average(r => r.Score), 2) : null,
                    revenue);
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        // CreatedAt is always stored as UTC (DateTimeOffset.UtcNow) — grouping directly on
        // .Year/.Month skews the trend Owner sees around every month boundary (e.g. a ticket
        // bought 2026-02-01 00:30 VN time is 2026-01-31 17:30 UTC, and would land in January
        // instead of February). Convert to VN local (UTC+7, same offset VnPayService uses) before
        // extracting the calendar month.
        var vnOffset = TimeSpan.FromHours(7);
        var nowVn = now.ToOffset(vnOffset);

        var trendMonths = Enumerable.Range(0, 6)
            .Select(i => nowVn.AddMonths(-i))
            .Select(d => (d.Year, d.Month))
            .Reverse()
            .ToList();

        var revenueTrend = trendMonths
            .Select(ym =>
            {
                var monthTickets = tickets.Where(t =>
                {
                    var d = t.CreatedAt.ToOffset(vnOffset);
                    return d.Year == ym.Year && d.Month == ym.Month;
                }).ToList();
                var monthFnb = fnbOrders.Where(o =>
                {
                    var d = o.CreatedAt.ToOffset(vnOffset);
                    return d.Year == ym.Year && d.Month == ym.Month;
                }).ToList();
                return new RevenueMonthDto(
                    ym.Year,
                    ym.Month,
                    monthFnb.Sum(o => o.TotalAmount),
                    monthTickets.Where(IsOffline).Sum(TicketAmount),
                    monthTickets.Where(t => !IsOffline(t)).Sum(TicketAmount));
            })
            .ToList();

        var ticketRevenue = tickets.Sum(TicketAmount);
        var fnbRevenue = fnbOrders.Sum(o => o.TotalAmount);

        return new OwnerAnalyticsDto(
            TotalShows: shows.Count,
            UpcomingShows: shows.Count(s => s.ScheduledStart > now
                && s.Status is LoungeShowStatus.Published or LoungeShowStatus.Pending or LoungeShowStatus.Draft),
            PastShows: shows.Count(s => s.Status is LoungeShowStatus.Ended or LoungeShowStatus.Cancelled
                || s.ScheduledStart <= now),
            TotalTicketsSold: tickets.Count,
            OfflineTicketsSold: tickets.Count(IsOffline),
            OnlineTicketsSold: tickets.Count(t => !IsOffline(t)),
            TotalRevenue: ticketRevenue + fnbRevenue,
            TicketRevenue: ticketRevenue,
            FnbRevenue: fnbRevenue,
            AverageRating: ratings.Count > 0 ? (decimal?)Math.Round(ratings.Average(r => r.Score), 2) : null,
            TotalRatings: ratings.Count,
            PendingArtistPayoutCount: pendingPayoutDonations.Count,
            PendingArtistPayoutAmount: pendingPayoutDonations.Sum(d => d.Net),
            RevenueTrend: revenueTrend,
            TopShows: topShows);
    }
}
