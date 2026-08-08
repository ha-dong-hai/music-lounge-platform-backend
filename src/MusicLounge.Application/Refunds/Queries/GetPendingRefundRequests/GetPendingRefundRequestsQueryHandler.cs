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

        var pending = await _uow.Repository<RefundRequest, int>()
            .FindAsync(r => r.Status == RefundRequestStatus.Pending, ct);

        var ordered = pending.OrderByDescending(r => r.Id).ToList();
        var items = ordered
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new RefundRequestDto(
                r.Id, r.PaymentId, r.RequestedBy, r.Reason, r.AmountRequested,
                r.AmountApproved, r.RefundPercentage, r.Status, r.CreatedAt, r.ResolvedAt))
            .ToList();

        return new PaginatedResult<RefundRequestDto>(items, page, size, ordered.Count);
    }
}
