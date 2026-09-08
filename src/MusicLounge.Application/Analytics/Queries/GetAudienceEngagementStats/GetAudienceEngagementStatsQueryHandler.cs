using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Analytics.Queries.GetAudienceEngagementStats;

internal sealed class GetAudienceEngagementStatsQueryHandler
    : IRequestHandler<GetAudienceEngagementStatsQuery, AudienceEngagementStatsDto>
{
    // Same VN-local (UTC+7) convention as GetAdminPlatformOverviewQueryHandler.
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public GetAudienceEngagementStatsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<AudienceEngagementStatsDto> Handle(
        GetAudienceEngagementStatsQuery request, CancellationToken ct)
    {
        DateTimeOffset from, to;
        if (request.From.HasValue && request.To.HasValue)
        {
            from = request.From.Value;
            to = request.To.Value;
        }
        else
        {
            var nowVn = DateTimeOffset.UtcNow.ToOffset(VnOffset);
            var monthStart = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, VnOffset);
            from = request.From ?? monthStart;
            to = request.To ?? monthStart.AddMonths(1).AddTicks(-1);
        }

        var follows = await _uow.Repository<Follow, int>().FindAsync(f => f.CreatedAt >= from, ct);
        var newFollows = follows.Count(f => f.CreatedAt <= to);

        var wishlists = await _uow.Repository<ShowWishlist, int>().FindAsync(w => w.CreatedAt >= from, ct);
        var newWishlists = wishlists.Count(w => w.CreatedAt <= to);

        var ratings = await _uow.Repository<LoungeShowRating, int>()
            .FindAsync(r => !r.IsRemoved && r.CreatedAt >= from, ct);
        var newRatings = ratings.Count(r => r.CreatedAt <= to);

        // "Tỷ lệ quay lại" khong co dinh nghia toan hoc ro rang trong mo ta Jira — chon: trong so
        // Audience da mua it nhat 1 ve (Confirmed/Used) trong ky, ty le nguoi mua ve cho >= 2 su
        // kien KHAC NHAU cung trong ky do (repeat-engagement trong ky, khong truy ngươc lich su
        // truoc ky) — don gian, kiem chung duoc, dung dung du lieu trong pham vi tu/den da chon.
        var ticketsInPeriod = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used)
                && t.CreatedAt >= from, ct);
        var buyers = ticketsInPeriod
            .Where(t => t.CreatedAt <= to && t.BuyerId.HasValue)
            .GroupBy(t => t.BuyerId!.Value)
            .Select(g => g.Select(t => t.ShowId).Distinct().Count())
            .ToList();
        var returnRate = buyers.Count == 0
            ? 0m
            : Math.Round(100m * buyers.Count(distinctShows => distinctShows >= 2) / buyers.Count, 2);

        return new AudienceEngagementStatsDto(
            PeriodFrom: from,
            PeriodTo: to,
            NewFollowsInPeriod: newFollows,
            NewWishlistsInPeriod: newWishlists,
            NewRatingsInPeriod: newRatings,
            ReturnRatePercent: returnRate);
    }
}
