using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.LoungeShows.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.LoungeShows.Queries.GetLoungeShowDetail;

internal sealed class GetLoungeShowDetailQueryHandler
    : IRequestHandler<GetLoungeShowDetailQuery, LoungeShowDetailDto>
{
    private readonly ILoungeShowRepository _showRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _jobs;
    private readonly IUnitOfWork _uow;

    public GetLoungeShowDetailQueryHandler(
        ILoungeShowRepository showRepo,
        ICurrentUserService currentUser,
        IBackgroundJobService jobs,
        IUnitOfWork uow)
    {
        _showRepo = showRepo;
        _currentUser = currentUser;
        _jobs = jobs;
        _uow = uow;
    }

    public async Task<LoungeShowDetailDto> Handle(
        GetLoungeShowDetailQuery request, CancellationToken ct)
    {
        var show = await _showRepo.GetByIdWithDetailsAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.LoungeShow), request.ShowId);

        var isOwnerOfShow = _currentUser.IsAuthenticated && show.Lounge.OwnerId == _currentUser.UserId;
        if (show.Status == LoungeShowStatus.Draft
            && _currentUser.Role is not Roles.Admin and not Roles.Staff
            && !isOwnerOfShow)
            throw new NotFoundException(nameof(Domain.Entities.LoungeShow), request.ShowId);

        if (_currentUser.IsAuthenticated)
            _jobs.EnqueueLogUserBehaviour(_currentUser.UserId, request.ShowId, BehaviourAction.ViewEvent);

        var wishlisted = _currentUser.IsAuthenticated
            ? await _showRepo.GetWishlistedShowIdsAsync(_currentUser.UserId, ct)
            : (IReadOnlySet<int>)new HashSet<int>();

        bool? userHasTicket = null;
        bool? userHasRated = null;
        if (_currentUser.IsAuthenticated)
        {
            userHasTicket = await _uow.Repository<Ticket, Guid>()
                .AnyAsync(t => t.ShowId == request.ShowId
                    && t.BuyerId == _currentUser.UserId
                    && (t.Status == TicketStatus.Confirmed || t.Status == TicketStatus.Used), ct);
            userHasRated = await _uow.Repository<LoungeShowRating, int>()
                .AnyAsync(r => r.LoungeShowId == request.ShowId
                    && r.UserId == _currentUser.UserId, ct);
        }

        var priceIds = show.TicketTiers
            .SelectMany(t => t.Prices)
            .Select(p => p.Id)
            .ToList();
        var soldAndHeld = await _showRepo.GetSoldAndHeldCountsByPriceAsync(priceIds, ct);

        return show.ToDetailDto(wishlisted, userHasTicket, userHasRated, soldAndHeld);
    }
}
