using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Settlements.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Settlements.Queries.GetSettlementsPendingReview;

internal sealed class GetSettlementsPendingReviewQueryHandler
    : IRequestHandler<GetSettlementsPendingReviewQuery, PaginatedResult<SettlementReviewDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetSettlementsPendingReviewQueryHandler(IUnitOfWork uow, ISystemConfigService config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<PaginatedResult<SettlementReviewDto>> Handle(
        GetSettlementsPendingReviewQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var (parked, total) = await _uow.Repository<Settlement, int>().GetPagedAsync(
            s => s.Status == SettlementStatus.PendingReview, s => s.Id, page, size, ct);

        if (parked.Count == 0)
            return new PaginatedResult<SettlementReviewDto>([], page, size, total);

        // Cùng ngưỡng mà SettlementReleaseJob dùng để park — Admin phải thấy đúng con số đã giữ
        // khoản này lại, không phải một mốc khác.
        var threshold = await _config.GetDecimalAsync(
            ConfigKeys.SettlementCompletionThresholdPct, 0.70m, ct);

        var paymentIds = parked.Select(s => s.PaymentId).Distinct().ToList();

        // Buổi diễn đi qua vé: một Payment chỉ thuộc đúng một buổi diễn (PurchaseTicket tạo mọi vé
        // từ cùng một hold).
        var tickets = await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => paymentIds.Contains(t.PaymentId!.Value), ct);
        var showIdByPayment = tickets
            .Where(t => t.PaymentId.HasValue)
            .GroupBy(t => t.PaymentId!.Value)
            .ToDictionary(g => g.Key, g => g.First().ShowId);

        var showIds = showIdByPayment.Values.Distinct().ToList();
        var shows = showIds.Count == 0
            ? []
            : await _uow.Repository<LoungeShow, int>().FindAsync(s => showIds.Contains(s.Id), ct);
        var showById = shows.ToDictionary(s => s.Id);

        // Một khoản vừa bị giữ vì thời lượng, vừa có yêu cầu hoàn tiền đang chờ, thì Admin cần biết
        // trước khi bấm chi trả — chi xong rồi mới duyệt hoàn là phải viết bút toán đảo bằng tay.
        var refunds = await _uow.Repository<RefundRequest, int>()
            .FindAsync(r => paymentIds.Contains(r.PaymentId) && r.Status == RefundRequestStatus.Pending, ct);
        var paymentsWithPendingRefund = refunds.Select(r => r.PaymentId).ToHashSet();

        var items = parked.Select(s =>
        {
            showIdByPayment.TryGetValue(s.PaymentId, out var showId);
            var show = showId != 0 && showById.TryGetValue(showId, out var found) ? found : null;
            var evidence = ShowCompletion.Evaluate(show);

            return new SettlementReviewDto(
                SettlementId: s.Id,
                OwnerId: s.OwnerId,
                PaymentId: s.PaymentId,
                ReleaseType: s.ReleaseType.ToString(),
                GrossAmount: s.GrossAmount,
                NetAmount: s.NetAmount,
                ScheduledAt: s.ScheduledAt,
                ShowId: show?.Id,
                ShowName: show?.Name,
                ScheduledStart: show?.ScheduledStart,
                ScheduledEnd: show is null ? null : ShowSchedule.EffectiveEnd(show),
                ActualStart: show?.ActualStart,
                ActualEnd: show?.ActualEnd,
                Verdict: evidence.Verdict.ToString(),
                Ratio: evidence.Ratio,
                Threshold: threshold,
                HasPendingRefund: paymentsWithPendingRefund.Contains(s.PaymentId));
        }).ToList();

        return new PaginatedResult<SettlementReviewDto>(items, page, size, total);
    }
}
