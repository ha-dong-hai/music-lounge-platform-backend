using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Refunds.Queries.GetPendingRefundRequests;

internal sealed class GetPendingRefundRequestsQueryHandler
    : IRequestHandler<GetPendingRefundRequestsQuery, PaginatedResult<RefundRequestDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public GetPendingRefundRequestsQueryHandler(IUnitOfWork uow, ISystemConfigService config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<PaginatedResult<RefundRequestDto>> Handle(
        GetPendingRefundRequestsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var (pending, total) = await _uow.Repository<RefundRequest, int>().GetPagedAsync(
            r => r.Status == RefundRequestStatus.Pending, r => r.Id, page, size, ct);

        // Cung mot con so voi cai da hua voi nguoi mua o GetMyRefundRequests va cai
        // RefundSlaBreachAlertJob dung de canh bao — Admin phai nhin thay dung han ma nguoi mua
        // dang duoc hen, chu khong phai mot moc khac.
        var slaHours = await _config.GetIntAsync(ConfigKeys.RefundSlaHours, 72, ct);

        var items = pending
            .Select(r => new RefundRequestDto(
                r.Id, r.PaymentId, r.RequestedBy, r.Reason, r.AmountRequested,
                r.AmountApproved, r.RefundPercentage, r.Status,
                new DateTimeOffset(r.CreatedAt, TimeSpan.Zero), r.ResolvedAt,
                new DateTimeOffset(r.CreatedAt, TimeSpan.Zero).AddHours(slaHours)))
            .ToList();

        return new PaginatedResult<RefundRequestDto>(items, page, size, total);
    }
}
