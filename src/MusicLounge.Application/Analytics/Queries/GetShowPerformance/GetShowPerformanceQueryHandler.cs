using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Analytics.Queries.GetShowPerformance;

internal sealed class GetShowPerformanceQueryHandler
    : IRequestHandler<GetShowPerformanceQuery, ShowPerformanceDto>
{
    private static readonly BehaviourAction[] ViewActions =
        [BehaviourAction.ViewEvent, BehaviourAction.ViewAfterWishlist];

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetShowPerformanceQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<ShowPerformanceDto> Handle(GetShowPerformanceQuery request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<Domain.Entities.MusicLounge, int>()
            .GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.MusicLounge), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem thống kê của sự kiện này.");

        // Only counts logged-in, AiConsent==true visits (LogUserBehaviourJob's own gate) —
        // anonymous browsing and non-consenting users are not represented here. This is the only
        // view-tracking mechanism this codebase has; a true all-traffic page-view counter would
        // need a separate, consent-independent tracking pipeline that doesn't exist yet.
        var logs = await _uow.Repository<UserBehaviourLog, int>()
            .FindAsync(l => l.LoungeShowId == request.ShowId, ct);

        var viewLogs = logs.Where(l => ViewActions.Contains(l.Action)).ToList();
        var totalPageViews = viewLogs.Count;
        var uniqueViewers = viewLogs.Select(l => l.UserId).Distinct().Count();

        var uniquePurchasers = logs
            .Where(l => l.Action == BehaviourAction.PurchaseTicket)
            .Select(l => l.UserId)
            .Distinct()
            .Count();
        var conversionRate = uniqueViewers > 0 ? Math.Round((decimal)uniquePurchasers / uniqueViewers, 4) : 0m;

        var liveViewers = logs
            .Where(l => l.Action == BehaviourAction.WatchLivestream)
            .Select(l => l.UserId)
            .Distinct()
            .Count();

        var tickets = await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => t.ShowId == request.ShowId
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);
        var ticketsSold = tickets.Count;
        var ticketsCheckedIn = tickets.Count(t => t.Status == TicketStatus.Used);
        var checkInRate = ticketsSold > 0 ? Math.Round((decimal)ticketsCheckedIn / ticketsSold, 4) : 0m;

        return new ShowPerformanceDto(
            ShowId: show.Id,
            ShowName: show.Name,
            TotalPageViews: totalPageViews,
            UniqueViewers: uniqueViewers,
            TicketsSold: ticketsSold,
            TicketsCheckedIn: ticketsCheckedIn,
            CheckInRate: checkInRate,
            UniquePurchasers: uniquePurchasers,
            ConversionRate: conversionRate,
            LiveViewers: liveViewers);
    }
}
