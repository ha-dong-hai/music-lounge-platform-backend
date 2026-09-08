using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetAdminContentOverview;

internal sealed class GetAdminContentOverviewQueryHandler
    : IRequestHandler<GetAdminContentOverviewQuery, AdminContentOverviewDto>
{
    private const int TopVenuesCount = 10;

    // Same VN-local (UTC+7) convention as GetAdminPlatformOverviewQueryHandler.
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public GetAdminContentOverviewQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<AdminContentOverviewDto> Handle(
        GetAdminContentOverviewQuery request, CancellationToken ct)
    {
        var pendingEventsCount = await _uow.Repository<LoungeShow, int>().CountAsync(
            s => s.Status == LoungeShowStatus.Pending, ct);

        var unresolvedComplaintsCount = await _uow.Repository<Complaint, int>().CountAsync(
            c => c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Investigating, ct);

        // "Vi phạm trong tháng" reads as the current calendar month (VN local) — a fixed
        // snapshot metric for the dashboard, same as ActiveVenuesCount in
        // GetAdminPlatformOverviewQueryHandler, not a caller-selected range.
        var nowVn = DateTimeOffset.UtcNow.ToOffset(VnOffset);
        var monthStart = new DateTimeOffset(nowVn.Year, nowVn.Month, 1, 0, 0, 0, VnOffset);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        // Filter by equality server-side, then narrow to the date range client-side — same
        // SQLite-translation caution documented throughout this codebase's other handlers.
        var penaltiesThisMonth = await _uow.Repository<VenuePenalty, int>().FindAsync(
            p => p.IssuedAt >= monthStart, ct);
        var violationsThisMonthCount = penaltiesThisMonth.Count(p => p.IssuedAt <= monthEnd);

        var lounges = await _uow.Repository<MusicLoungeEntity, int>().FindAsync(_ => true, ct);
        var topVenuesByReputation = lounges
            .OrderByDescending(l => l.ReputationScore)
            .Take(TopVenuesCount)
            .Select(l => new VenueReputationRankDto(l.Id, l.Name, l.ReputationScore))
            .ToList();

        return new AdminContentOverviewDto(
            PendingEventsCount: pendingEventsCount,
            UnresolvedComplaintsCount: unresolvedComplaintsCount,
            ViolationsThisMonthCount: violationsThisMonthCount,
            TopVenuesByReputation: topVenuesByReputation);
    }
}
