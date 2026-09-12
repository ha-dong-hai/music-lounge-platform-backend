using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Refunds.Queries.GetMyRefundRequests;

internal sealed class GetMyRefundRequestsQueryHandler
    : IRequestHandler<GetMyRefundRequestsQuery, PaginatedResult<RefundRequestDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;

    public GetMyRefundRequestsQueryHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<PaginatedResult<RefundRequestDto>> Handle(
        GetMyRefundRequestsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var mine = await _uow.Repository<RefundRequest, int>()
            .FindAsync(r => r.RequestedBy == _currentUser.UserId, ct);

        // Cung mot nguon voi RefundSlaBreachAlertJob — cai canh bao Admin va cai hua voi nguoi mua
        // phai la cung mot con so, neu khong thi mot ben se im lang trong khi ben kia da tre han.
        var slaHours = await _config.GetIntAsync(ConfigKeys.RefundSlaHours, 72, ct);

        // MLACP-387: nguoi mua phai thay yeu cau nao dang cho ho khai tai khoan nhan hoan.
        var paymentIds = mine.Select(r => r.PaymentId).Distinct().ToList();
        var payments = (await _uow.Repository<Payment, int>().FindAsync(p => paymentIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);
        var windowDays = await RefundGatewayWindow.WindowDaysAsync(_config, ct);
        var now = DateTimeOffset.UtcNow;

        var ordered = mine.OrderByDescending(r => r.CreatedAt).ToList();
        var items = ordered
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new RefundRequestDto(
                r.Id, r.PaymentId, r.RequestedBy, r.Reason, r.AmountRequested,
                r.AmountApproved, r.RefundPercentage, r.Status,
                new DateTimeOffset(r.CreatedAt, TimeSpan.Zero), r.ResolvedAt,
                r.Status == RefundRequestStatus.Pending
                    ? new DateTimeOffset(r.CreatedAt, TimeSpan.Zero).AddHours(slaHours)
                    : null,
                RefundGatewayWindow.NeedsPayoutAccount(r, payments.GetValueOrDefault(r.PaymentId), windowDays, now),
                r.PayoutBankName, r.PayoutAccountNumber, r.PayoutAccountHolder, r.PayoutConsentAt))
            .ToList();

        return new PaginatedResult<RefundRequestDto>(items, page, size, ordered.Count);
    }
}
