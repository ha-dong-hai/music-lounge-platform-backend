using MediatR;
using MusicLounge.Application.Analytics.DTOs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Analytics.Queries.GetOwnerArtistDonationStats;

internal sealed class GetOwnerArtistDonationStatsQueryHandler
    : IRequestHandler<GetOwnerArtistDonationStatsQuery, OwnerArtistDonationReportDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetOwnerArtistDonationStatsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<OwnerArtistDonationReportDto> Handle(
        GetOwnerArtistDonationStatsQuery request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền xem thống kê donate của venue này.");

        var shows = await _uow.Repository<LoungeShow, int>()
            .FindAsync(s => s.LoungeId == request.LoungeId, ct);
        var showIds = shows.Select(s => s.Id).ToHashSet();

        var performances = await _uow.Repository<Performance, int>()
            .FindAsync(p => showIds.Contains(p.LoungeShowId), ct);
        var performanceIds = performances.Select(p => p.Id).ToHashSet();
        var performerIdByPerformance = performances.ToDictionary(p => p.Id, p => p.PerformerId);
        var showIdByPerformance = performances.ToDictionary(p => p.Id, p => p.LoungeShowId);

        // "Tổng donate" = tiền đã thực sự thu qua VNPay (PaymentConfirmedAt != null), bất kể đã
        // chuyển cho nghệ sĩ (chặng 2) hay chưa — cùng định nghĩa "donate nhận" đã dùng ở
        // GetOwnerRevenueReportQueryHandler (MLACP-162), tránh 2 báo cáo cho ra 2 con số khác nhau
        // cho cùng 1 khái niệm.
        var donations = await _uow.Repository<Donation, int>().FindAsync(
            d => performanceIds.Contains(d.PerformanceId) && d.PaymentConfirmedAt != null, ct);

        var performerIds = performances.Select(p => p.PerformerId).Distinct().ToList();
        var performers = await _uow.Repository<Performer, int>()
            .FindAsync(p => performerIds.Contains(p.Id), ct);
        var performerById = performers.ToDictionary(p => p.Id);

        var byArtist = donations
            .GroupBy(d => performerIdByPerformance[d.PerformanceId])
            .Select(g => new ArtistDonationStatsDto(
                PerformerId: g.Key,
                PerformerName: performerById.TryGetValue(g.Key, out var pf) ? pf.Name : "(đã xoá)",
                DonationCount: g.Count(),
                TotalGross: g.Sum(d => d.Gross),
                TotalNet: g.Sum(d => d.Net),
                ShowCount: g.Select(d => showIdByPerformance[d.PerformanceId]).Distinct().Count()))
            .OrderByDescending(a => a.TotalGross)
            .ToList();

        var top = byArtist.FirstOrDefault();

        return new OwnerArtistDonationReportDto(
            GrandTotalDonated: byArtist.Sum(a => a.TotalGross),
            TopPerformerId: top?.PerformerId,
            TopPerformerName: top?.PerformerName,
            ByArtist: byArtist);
    }
}
