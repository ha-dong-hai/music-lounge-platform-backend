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
        var loungeRepo = _uow.Repository<MusicLoungeEntity, Guid>();
        var venuesByStatus = new Dictionary<string, int>();
        foreach (var status in Enum.GetValues<LoungeStatus>())
            venuesByStatus[status.ToString()] = await loungeRepo.CountAsync(l => l.Status == status, ct);

        var totalVenues = venuesByStatus.Values.Sum();
        var operatingVenues = VenueLifecycle.Operating.Sum(s => venuesByStatus[s.ToString()]);

        var totalPublishedShows = await _uow.Repository<LoungeShow, Guid>().CountAsync(
            s => s.Status == LoungeShowStatus.Published
                || s.Status == LoungeShowStatus.Ongoing
                || s.Status == LoungeShowStatus.Ended, ct);

        var totalUsers = await _uow.Repository<User, Guid>().CountAsync(_ => true, ct);

        var totalTicketsSold = await _uow.Repository<Ticket, Guid>()
            .CountAsync(t => TicketRevenue.DaThuTien.Contains(t.Status), ct);

        // totalTicketsSold above counts both online (TicketHold) and walk-in/box-office (WalkIn)
        // sales — GMV must count the same two channels or the dashboard shows two numbers that
        // contradict each other for any venue selling mostly at the door.
        //
        // MLACP-616: GMV = tien da ban tru tien da hoan. Truoc day chi cong thanh toan Confirmed: hoan MOT PHAN thi
        // thanh toan van Confirmed nen van tinh du tien goc. Nay cong ca Confirmed lan Refunded roi tru moi khoan hoan
        // da duyet cua chung — hai cach cho cung ket qua voi hoan toan bo, nhung chi cach nay dung voi hoan mot phan.
        // Cung dinh nghia voi bang dieu khien Admin (GetAdminDashboardQueryHandler.BienDongTienAsync).
        var daBan = await _uow.Repository<Payment, Guid>().SumAsync(
            p => (p.Status == PaymentStatus.Confirmed || p.Status == PaymentStatus.Refunded)
                && (p.ReferenceType == "TicketHold" || p.ReferenceType == "WalkIn"),
            p => p.GrossAmount, ct);
        var daHoan = (await _uow.Repository<RefundRequest, Guid>().FindAsync(
                r => r.Status == RefundRequestStatus.Approved
                     && (r.Payment.Status == PaymentStatus.Confirmed || r.Payment.Status == PaymentStatus.Refunded)
                     && (r.Payment.ReferenceType == "TicketHold" || r.Payment.ReferenceType == "WalkIn"), ct))
            .Sum(r => r.AmountApproved ?? 0m);
        var totalGmv = daBan - daHoan;

        var totalDonationVolume = await _uow.Repository<Donation, Guid>().SumAsync(
            d => d.Status != DonationStatus.PendingPayment && d.Status != DonationStatus.Cancelled,
            d => d.Gross, ct);

        var pendingModerations = await _uow.Repository<EventModeration, Guid>()
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
