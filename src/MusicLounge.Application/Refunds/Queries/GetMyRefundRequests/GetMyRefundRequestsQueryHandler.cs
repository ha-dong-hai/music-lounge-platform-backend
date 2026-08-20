using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Refunds.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Refunds.Queries.GetMyRefundRequests;

internal sealed class GetMyRefundRequestsQueryHandler
    : IRequestHandler<GetMyRefundRequestsQuery, PaginatedResult<RefundRequestDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMyRefundRequestsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<RefundRequestDto>> Handle(
        GetMyRefundRequestsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);

        var mine = await _uow.Repository<RefundRequest, int>()
            .FindAsync(r => r.RequestedBy == _currentUser.UserId, ct);

        var ordered = mine.OrderByDescending(r => r.CreatedAt).ToList();
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
