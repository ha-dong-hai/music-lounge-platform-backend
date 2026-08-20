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

    public GetPendingRefundRequestsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<PaginatedResult<RefundRequestDto>> Handle(
        GetPendingRefundRequestsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var (pending, total) = await _uow.Repository<RefundRequest, int>().GetPagedAsync(
            r => r.Status == RefundRequestStatus.Pending, r => r.Id, page, size, ct);

        var items = pending
            .Select(r => new RefundRequestDto(
                r.Id, r.PaymentId, r.RequestedBy, r.Reason, r.AmountRequested,
                r.AmountApproved, r.RefundPercentage, r.Status, r.CreatedAt, r.ResolvedAt))
            .ToList();

        return new PaginatedResult<RefundRequestDto>(items, page, size, total);
    }
}
