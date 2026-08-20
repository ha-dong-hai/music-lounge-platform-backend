using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Queries.GetShowOrders;

internal sealed class GetShowOrdersQueryHandler
    : IRequestHandler<GetShowOrdersQuery, PaginatedResult<ShowOrderDto>>
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetShowOrdersQueryHandler(ITicketRepository ticketRepo, IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _ticketRepo = ticketRepo;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ShowOrderDto>> Handle(GetShowOrdersQuery request, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<Domain.Entities.MusicLounge, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.MusicLounge), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền xem đơn hàng của show này.");

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await _ticketRepo.GetByShowAsync(request.ShowId, page, pageSize, ct);

        return result.Map(t => new ShowOrderDto(
            t.Id,
            t.Buyer?.FullName,
            t.Buyer?.Email,
            t.Tier.Name,
            t.Price.Name,
            t.Price.Price,
            t.Status.ToString(),
            t.PurchaseChannel.ToString(),
            t.CreatedAt,
            t.PhysicalDetail?.CheckedInAt));
    }
}
