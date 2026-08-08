using MediatR;
using MusicLounge.Application.Analytics.DTOs;
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
        var totalVenues = await _uow.Repository<MusicLoungeEntity, int>().CountAsync(_ => true, ct);

        var totalPublishedShows = await _uow.Repository<LoungeShow, int>().CountAsync(
            s => s.Status == LoungeShowStatus.Published
                || s.Status == LoungeShowStatus.Ongoing
                || s.Status == LoungeShowStatus.Ended, ct);

        var totalUsers = await _uow.Repository<User, int>().CountAsync(_ => true, ct);

        var totalTicketsSold = await _uow.Repository<Ticket, Guid>()
            .CountAsync(t => t.Status == TicketStatus.Confirmed, ct);

        var totalGmv = await _uow.Repository<Payment, int>().SumAsync(
            p => p.Status == PaymentStatus.Confirmed && p.ReferenceType == "TicketHold",
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
            pendingModerations);
    }
}
