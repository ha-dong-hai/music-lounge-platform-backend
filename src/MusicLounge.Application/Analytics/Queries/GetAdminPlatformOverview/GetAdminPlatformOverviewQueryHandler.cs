using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetAdminPlatformOverview;

internal sealed class GetAdminPlatformOverviewQueryHandler
    : IRequestHandler<GetAdminPlatformOverviewQuery, AdminPlatformOverviewDto>
{
    // Same VN-local (UTC+7) convention as GetOwnerAnalyticsQueryHandler/GetOwnerRevenueReportQueryHandler.
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public GetAdminPlatformOverviewQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<AdminPlatformOverviewDto> Handle(
        GetAdminPlatformOverviewQuery request, CancellationToken ct)
    {
        // "Tổng buổi diễn TRONG THÁNG" reads as a default period, not an always-required filter —
        // default to the current calendar month (VN local) when Admin doesn't pick a custom range.
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

        // "Đang hoạt động" is a present-tense snapshot (how many venues can transact right now),
        // not a period metric — Warned still means open for business, just flagged; Pending/
        // Suspended/Locked are not currently operating.
        var activeVenuesCount = await _uow.Repository<MusicLoungeEntity, int>().CountAsync(
            l => l.Status == LoungeStatus.Approved || l.Status == LoungeStatus.Warned, ct);

        // Filter by equality server-side, then narrow to the date range client-side — combining an
        // enum/navigation equality filter with a DateTimeOffset range comparison in one query does
        // not reliably translate under the SQLite provider used in tests, same class of limitation
        // documented throughout this codebase's other repositories/jobs.
        var shows = await _uow.Repository<LoungeShow, int>().FindAsync(
            s => s.Status == LoungeShowStatus.Published
                || s.Status == LoungeShowStatus.Ongoing
                || s.Status == LoungeShowStatus.Ended, ct);
        var eventsInPeriodCount = shows.Count(s => s.ScheduledStart >= from && s.ScheduledStart <= to);

        var platformCredits = await _uow.Repository<LedgerEntry, int>().FindAsync(
            e => e.Account.OwnerType == AccountType.Platform && !e.IsDebit, ct);
        var platformRevenueInPeriod = platformCredits
            .Where(e => e.CreatedAt >= from && e.CreatedAt <= to)
            .Sum(e => e.Amount);

        // User.CreatedAt (AuditableEntity) is a plain DateTime — always written as
        // DateTime.UtcNow (ApplicationDbContext.SaveChangesAsync) — so compare against the UTC
        // instant of the DateTimeOffset range rather than the DateTimeOffset itself.
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;
        var audienceUsers = await _uow.Repository<User, int>().FindAsync(u => u.Role == UserRole.Audience, ct);
        var newAudienceSignupsInPeriod = audienceUsers
            .Count(u => u.CreatedAt >= fromUtc && u.CreatedAt <= toUtc);

        return new AdminPlatformOverviewDto(
            PeriodFrom: from,
            PeriodTo: to,
            ActiveVenuesCount: activeVenuesCount,
            EventsInPeriodCount: eventsInPeriodCount,
            PlatformRevenueInPeriod: platformRevenueInPeriod,
            NewAudienceSignupsInPeriod: newAudienceSignupsInPeriod);
    }
}
