using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerLivestreamHistory;

internal sealed class GetOwnerLivestreamHistoryQueryHandler
    : IRequestHandler<GetOwnerLivestreamHistoryQuery, PaginatedResult<LivestreamHistoryItemDto>>
{
    // Livestream khong con "dang dien ra" — bao gom ca Failed (MLACP-191: mat ket noi qua 5 phut
    // khong khoi phuc duoc) vao lich su, vi day van la 1 phien da thuc su xay ra, chi la bi cat
    // ngang boi su co ky thuat thay vi ket thuc binh thuong.
    private static readonly LivestreamStatus[] TerminalStatuses =
        [LivestreamStatus.Ended, LivestreamStatus.Terminated, LivestreamStatus.Failed];

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetOwnerLivestreamHistoryQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<LivestreamHistoryItemDto>> Handle(
        GetOwnerLivestreamHistoryQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem lịch sử livestream của venue này.");

        var shows = await _uow.Repository<LoungeShow, int>()
            .FindAsync(s => s.LoungeId == request.LoungeId, ct);
        var showIds = shows.Select(s => s.Id).ToHashSet();
        var showById = shows.ToDictionary(s => s.Id);

        var allLivestreams = await _uow.Repository<Livestream, int>()
            .FindAsync(l => showIds.Contains(l.LoungeShowId), ct);
        var livestreams = allLivestreams
            .Where(l => TerminalStatuses.Contains(l.Status))
            .ToList();
        var relevantShowIds = livestreams.Select(l => l.LoungeShowId).ToHashSet();

        // ---- PPV (Pay-Per-View) revenue: ve Livestream da thanh toan cho tung show ----
        var allTickets = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => relevantShowIds.Contains(t.ShowId)
                && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);
        var tierIds = allTickets.Select(t => t.TierId).Distinct().ToList();
        var livestreamTierIds = (await _uow.Repository<TicketTier, int>()
                .FindAsync(t => tierIds.Contains(t.Id) && t.AccessType == AccessType.Livestream, ct))
            .Select(t => t.Id)
            .ToHashSet();
        var ppvTickets = allTickets.Where(t => livestreamTierIds.Contains(t.TierId)).ToList();

        var priceIds = ppvTickets.Select(t => t.PriceId).Distinct().ToList();
        var priceById = (await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id, p => p.Price);
        var ppvRevenueByShow = ppvTickets
            .ToLookup(t => t.ShowId)
            .ToDictionary(g => g.Key, g => g.Sum(t => priceById.GetValueOrDefault(t.PriceId)));

        // ---- Tong donate trong phien: cung dinh nghia da dung o OwnerRevenueReportBuilder/
        // GetOwnerArtistDonationStatsQueryHandler (da thu tien qua VNPay, bat ke da tra nghe si hay chua) ----
        var performances = await _uow.Repository<Performance, int>()
            .FindAsync(p => relevantShowIds.Contains(p.LoungeShowId), ct);
        var performanceIds = performances.Select(p => p.Id).ToHashSet();
        var showIdByPerformance = performances.ToDictionary(p => p.Id, p => p.LoungeShowId);

        var donations = await _uow.Repository<Donation, int>().FindAsync(
            d => performanceIds.Contains(d.PerformanceId) && d.PaymentConfirmedAt != null, ct);
        var donationByShow = donations
            .Where(d => showIdByPerformance.ContainsKey(d.PerformanceId))
            .ToLookup(d => showIdByPerformance[d.PerformanceId])
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Gross));

        var ordered = livestreams
            .OrderByDescending(l => l.EndedAt)
            .ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LivestreamHistoryItemDto(
                LivestreamId: l.Id,
                ShowId: l.LoungeShowId,
                ShowName: showById.TryGetValue(l.LoungeShowId, out var show) ? show.Name : "(đã xoá)",
                StartedAt: l.StartedAt,
                EndedAt: l.EndedAt,
                PeakViewerCount: l.PeakViewerCount,
                TotalViews: l.TotalViews,
                PpvRevenue: ppvRevenueByShow.GetValueOrDefault(l.LoungeShowId),
                TotalDonations: donationByShow.GetValueOrDefault(l.LoungeShowId)))
            .ToList();

        return new PaginatedResult<LivestreamHistoryItemDto>(items, page, pageSize, ordered.Count);
    }
}
