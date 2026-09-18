using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetPlatformAnalytics;

internal sealed class GetPlatformAnalyticsQueryHandler
    : IRequestHandler<GetPlatformAnalyticsQuery, PlatformAnalyticsDto>
{
    private readonly IUnitOfWork _uow;

    public GetPlatformAnalyticsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PlatformAnalyticsDto> Handle(GetPlatformAnalyticsQuery request, CancellationToken ct)
    {
        // MLACP-452: dem theo tung trang thai roi SUY RA tong va so dang hoat dong tu cung bang dem — ba con so khong the
        // lech nhau. "Dang hoat dong" lay dung VenueLifecycle.Operating, dinh nghia chung ma danh sach cong khai va
        // /analytics/admin-overview cung dung; truoc day endpoint nay dem moi trang thai ma khong noi ro.
        var loungeRepo = _uow.Repository<MusicLoungeEntity, int>();
        var venuesByStatus = new Dictionary<string, int>();
        foreach (var status in Enum.GetValues<LoungeStatus>())
            venuesByStatus[status.ToString()] = await loungeRepo.CountAsync(l => l.Status == status, ct);

        var totalVenues = venuesByStatus.Values.Sum();
        var operatingVenues = VenueLifecycle.Operating.Sum(s => venuesByStatus[s.ToString()]);

        var totalPublishedShows = await _uow.Repository<LoungeShow, int>().CountAsync(
            s => s.Status == LoungeShowStatus.Published
                || s.Status == LoungeShowStatus.Ongoing
                || s.Status == LoungeShowStatus.Ended, ct);

        var totalUsers = await _uow.Repository<User, int>().CountAsync(_ => true, ct);

        var totalTicketsSold = await _uow.Repository<Ticket, Guid>()
            .CountAsync(t => t.Status == TicketStatus.Confirmed, ct);

        // totalTicketsSold above counts both online (TicketHold) and walk-in/box-office (WalkIn)
        // sales — GMV must count the same two channels or the dashboard shows two numbers that
        // contradict each other for any venue selling mostly at the door.
        var totalGmv = await _uow.Repository<Payment, int>().SumAsync(
            p => p.Status == PaymentStatus.Confirmed
                && (p.ReferenceType == "TicketHold" || p.ReferenceType == "WalkIn"),
            p => p.GrossAmount, ct);

        var totalDonationVolume = await _uow.Repository<Donation, int>().SumAsync(
            d => d.Status != DonationStatus.PendingPayment && d.Status != DonationStatus.Cancelled,
            d => d.Gross, ct);

        var pendingModerations = await _uow.Repository<EventModeration, int>()
            .CountAsync(m => m.AdminDecision == null, ct);

        return new PlatformAnalyticsDto(
            totalVenues,
            totalPublishedShows,
            totalUsers,
            totalTicketsSold,
            totalGmv,
            totalDonationVolume,
            pendingModerations,
            operatingVenues,
            venuesByStatus);
    }
}
