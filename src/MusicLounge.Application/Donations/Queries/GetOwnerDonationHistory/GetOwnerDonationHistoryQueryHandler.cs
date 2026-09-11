using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Donations.Queries.GetOwnerDonationHistory;

internal sealed class GetOwnerDonationHistoryQueryHandler
    : IRequestHandler<GetOwnerDonationHistoryQuery, OwnerDonationHistorySummaryDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;

    public GetOwnerDonationHistoryQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<OwnerDonationHistorySummaryDto> Handle(
        GetOwnerDonationHistoryQuery request, CancellationToken ct)
    {
        var lounges = await _uow.Repository<MusicLoungeEntity, int>()
            .FindAsync(l => l.OwnerId == _currentUser.UserId, ct);
        var loungeIds = lounges.Select(l => l.Id).ToHashSet();

        var shows = await _uow.Repository<LoungeShow, int>()
            .FindAsync(s => loungeIds.Contains(s.LoungeId), ct);
        var showIds = shows.Select(s => s.Id).ToHashSet();
        var showById = shows.ToDictionary(s => s.Id);

        var performances = await _uow.Repository<Performance, int>()
            .FindAsync(p => showIds.Contains(p.LoungeShowId), ct);
        var performanceIds = performances.Select(p => p.Id).ToHashSet();
        var showIdByPerformance = performances.ToDictionary(p => p.Id, p => p.LoungeShowId);
        var performerIdByPerformance = performances.ToDictionary(p => p.Id, p => p.PerformerId);

        var performerIds = performances.Select(p => p.PerformerId).Distinct().ToList();
        var performerById = (await _uow.Repository<Performer, int>()
                .FindAsync(p => performerIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);

        // "Donate đã nhận" = Owner đã thực sự xác nhận nhận tiền (OwnerReceived) hoặc đã trả xong
        // cho nghệ sĩ (PerformerPaid) — donate còn PendingPayment/PendingOwnerAck/Cancelled chưa
        // từng "về tay" Owner, không thuộc phạm vi lịch sử này.
        var allDonations = await _uow.Repository<Donation, int>().FindAsync(
            d => performanceIds.Contains(d.PerformanceId)
                && (d.Status == DonationStatus.OwnerReceived || d.Status == DonationStatus.PerformerPaid), ct);

        bool InRange(DateTimeOffset? d) =>
            !d.HasValue
            || ((!request.From.HasValue || d >= request.From.Value)
                && (!request.To.HasValue || d <= request.To.Value));

        var donations = allDonations.Where(d => InRange(d.PaymentConfirmedAt)).ToList();

        // MLACP-362: cung mot moc voi nhac nho, canh cao, tu xac nhan va dieu kien khieu nai — luc
        // phong tra that su nhan tien. Truoc day tinh tu luc VNPay xac nhan, nen mot khoan nen tang
        // chuyen muon van hien "qua han" du phong tra moi nhan tien hom qua.
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(_config, ct);
        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(
            _uow, donations.Select(d => d.Id).ToList(), ct);
        var now = DateTimeOffset.UtcNow;

        DateTimeOffset? PayoutDueAtOf(Donation d) =>
            d.Status == DonationStatus.PerformerPaid
                ? null
                : DonationPayoutDeadline.DueAt(DonationPayoutDeadline.ReceivedAt(d, releaseTimes), holdDays);

        string PayoutStatusOf(Donation d)
        {
            if (d.Status == DonationStatus.PerformerPaid) return "Paid";
            return PayoutDueAtOf(d) is { } dueAt && now > dueAt ? "Overdue" : "WithinHoldPeriod";
        }

        var ordered = donations.OrderByDescending(d => d.PaymentConfirmedAt ?? d.CreatedAt).ToList();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var pagedItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new OwnerDonationHistoryItemDto(
                Id: d.Id,
                PerformerName: performerIdByPerformance.TryGetValue(d.PerformanceId, out var performerId)
                    && performerById.TryGetValue(performerId, out var performer)
                        ? performer.Name : "(đã xoá)",
                ShowName: showIdByPerformance.TryGetValue(d.PerformanceId, out var showId)
                    && showById.TryGetValue(showId, out var show)
                        ? show.Name : "(đã xoá)",
                Gross: d.Gross,
                Net: d.Net,
                PayoutStatus: PayoutStatusOf(d),
                PaymentConfirmedAt: d.PaymentConfirmedAt,
                OwnerPaidAt: d.OwnerPaidAt,
                PayoutDueAt: PayoutDueAtOf(d),
                CreatedAt: d.CreatedAt))
            .ToList();

        var from = request.From ?? donations.MinBy(d => d.PaymentConfirmedAt ?? d.CreatedAt)?.CreatedAt ?? now;
        var to = request.To ?? now;

        return new OwnerDonationHistorySummaryDto(
            PeriodFrom: from,
            PeriodTo: to,
            TotalCount: donations.Count,
            TotalGross: donations.Sum(d => d.Gross),
            PaidCount: donations.Count(d => d.Status == DonationStatus.PerformerPaid),
            WithinHoldCount: donations.Count(d => PayoutStatusOf(d) == "WithinHoldPeriod"),
            OverdueCount: donations.Count(d => PayoutStatusOf(d) == "Overdue"),
            Items: new PaginatedResult<OwnerDonationHistoryItemDto>(pagedItems, page, pageSize, donations.Count));
    }
}
