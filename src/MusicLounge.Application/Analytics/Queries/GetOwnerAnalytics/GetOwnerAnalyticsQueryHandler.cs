using MusicLounge.Application.FnbOrders;
using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Constants;
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
        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền xem thống kê của venue này.");

        var shows = await _uow.Repository<LoungeShow, Guid>()
            .FindAsync(s => s.LoungeId == request.LoungeId, ct);
        var showIds = shows.Select(s => s.Id).ToHashSet();
        var now = DateTimeOffset.UtcNow;

        // MLACP-659: lọc theo kỳ bằng THỜI ĐIỂM PHÁT SINH (vé: lúc mua; gọi món: lúc tạo đơn) — cùng quy tắc với
        // OwnerRevenueReportBuilder, nên các ô số liệu và báo cáo doanh thu cùng trang ra cùng một số cho cùng một kỳ.
        // Lọc sau khi nạp vì so sánh khoảng DateTimeOffset không dịch ổn định trên SQLite của bộ test.
        // Không truyền kỳ thì giữ nguyên hành vi cũ: mọi thời gian.
        bool InRange(DateTimeOffset d) =>
            (!request.From.HasValue || d >= request.From.Value) && (!request.To.HasValue || d <= request.To.Value);

        var tickets = (await _uow.Repository<Ticket, Guid>()
                .FindAsync(t => showIds.Contains(t.ShowId) && t.Status == TicketStatus.Confirmed, ct))
            .Where(t => InRange(t.CreatedAt))
            .ToList();

        var priceIds = tickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await _uow.Repository<TicketPrice, Guid>()
            .FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id);

        var tiers = await _uow.Repository<TicketTier, Guid>()
            .FindAsync(t => showIds.Contains(t.LoungeShowId), ct);
        var tierById = tiers.ToDictionary(t => t.Id);
        var tiersByShow = tiers.ToLookup(t => t.LoungeShowId);

        decimal TicketAmount(Ticket t) => priceById.TryGetValue(t.PriceId, out var p) ? p.Price : 0m;
        bool IsOffline(Ticket t) => tierById.TryGetValue(t.TierId, out var tier) && tier.AccessType == AccessType.Physical;

        var ticketsByShow = tickets.ToLookup(t => t.ShowId);

        var ratings = await _uow.Repository<LoungeShowRating, Guid>()
            .FindAsync(r => showIds.Contains(r.LoungeShowId) && !r.IsRemoved, ct);
        var ratingsByShow = ratings.ToLookup(r => r.LoungeShowId);

        // MLACP-349: dem theo "da tra tien" (FnbOrderPayments), khong theo buoc cuoi cua bep — don tra
        // truoc qua VNPay la doanh thu that tu luc IPN xac nhan.
        var loungeFnbOrders = await _uow.Repository<FnbOrder, Guid>()
            .FindAsync(o => o.LoungeId == request.LoungeId && o.Status != FnbOrderStatus.Cancelled, ct);
        var paidFnbOrderIds = await FnbOrderPayments.ConfirmedOrderIdsAsync(
            _uow, loungeFnbOrders.Select(o => o.Id).ToList(), ct);
        // FnbOrder.CreatedAt là DateTime lưu UTC — đổi tường minh, không để phép đổi ngầm lấy múi giờ máy chủ.
        var fnbOrders = loungeFnbOrders
            .Where(o => FnbOrderPayments.IsPaid(o, paidFnbOrderIds.Contains(o.Id)))
            .Where(o => InRange(new DateTimeOffset(DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc))))
            .ToList();

        var performances = await _uow.Repository<Performance, Guid>()
            .FindAsync(p => showIds.Contains(p.LoungeShowId), ct);
        var performerIds = performances.Select(p => p.PerformerId).Distinct().ToList();
        var performers = await _uow.Repository<Performer, Guid>().FindAsync(p => performerIds.Contains(p.Id), ct);
        var performerById = performers.ToDictionary(p => p.Id);
        var mainPerformerByShow = performances
            .Where(p => p.Role == PerformerRole.Main)
            .GroupBy(p => p.LoungeShowId)
            .ToDictionary(g => g.Key, g => performerById.TryGetValue(g.First().PerformerId, out var pf) ? pf.Name : null);

        var performanceIds = performances.Select(p => p.Id).ToHashSet();
        var pendingPayoutDonations = (await _uow.Repository<Donation, Guid>()
            .FindAsync(d => performanceIds.Contains(d.PerformanceId) && d.Status == DonationStatus.OwnerReceived, ct))
            .ToList();

        // Có kỳ thì chỉ xếp hạng buổi có bán được vé trong kỳ — bản không lọc sẽ chen buổi 0đ vào top khi kỳ ngắn.
        var hasRange = request.From.HasValue || request.To.HasValue;
        var topShows = showIds
            .Where(id => !hasRange || ticketsByShow[id].Any())
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
        
        // MLACP-659: có kỳ thì vẽ đúng các tháng của kỳ (cuối kỳ mặc định là hôm nay). Trần 24 tháng: kỳ dài hơn chỉ vẽ 24
        // tháng cuối — cột hẹp tới mức không đọc được. Nâng cấp khi cần: gom theo quý cho kỳ dài.
        var endVn = (request.To ?? now).ToOffset(vnOffset);
        var startVn = request.From?.ToOffset(vnOffset) ?? endVn.AddMonths(-5);
        var monthSpan = Math.Clamp((endVn.Year - startVn.Year) * 12 + endVn.Month - startVn.Month + 1, 1, 24);
        var trendMonths = Enumerable.Range(0, monthSpan)
            .Select(i => endVn.AddMonths(-i))
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
                    var d = new DateTimeOffset(o.CreatedAt, TimeSpan.Zero).ToOffset(vnOffset);
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
