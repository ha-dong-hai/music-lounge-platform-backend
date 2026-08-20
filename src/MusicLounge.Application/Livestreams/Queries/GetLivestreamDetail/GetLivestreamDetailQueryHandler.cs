using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

internal sealed class GetLivestreamDetailQueryHandler : IRequestHandler<GetLivestreamDetailQuery, LivestreamDetailDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBackgroundJobService _backgroundJobs;

    public GetLivestreamDetailQueryHandler(
        IUnitOfWork uow,
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser,
        IBackgroundJobService backgroundJobs)
    {
        _uow = uow;
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<LivestreamDetailDto> Handle(GetLivestreamDetailQuery request, CancellationToken ct)
    {
        var livestream = await _livestreamRepo.GetByIdWithDetailsAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException("Livestream", request.LivestreamId);

        // HLS URL is only visible to users with a valid livestream ticket, or to Staff/Owner of
        // THIS venue who need to monitor the stream — was previously any Staff/Admin account
        // regardless of venue, letting Staff of venue A watch venue B's paid livestream for free.
        var userHasAccess = _currentUser.Role == Roles.Admin;
        var isGenuineTicketHolder = false;
        if (!userHasAccess)
        {
            var lounge = await _uow.Repository<MusicLoungeEntity, int>()
                .GetByIdAsync(livestream.LoungeShow.LoungeId, ct);
            var isVenueOperator = lounge is not null
                && VenueOperatorAccess.CanOperate(_currentUser, livestream.LoungeShow.LoungeId, lounge.OwnerId);
            isGenuineTicketHolder = !isVenueOperator && _currentUser.IsAuthenticated
                && await _livestreamRepo.HasViewerAccessAsync(request.LivestreamId, _currentUser.UserId, ct);
            userHasAccess = isVenueOperator || isGenuineTicketHolder;
        }

        // Only a real ticket-holding viewer actually "watching" counts as a recommendation signal —
        // Admin/Staff/Owner hitting this endpoint to monitor their own stream isn't behavioural
        // interest and would otherwise pollute the collaborative-filtering matrix.
        if (isGenuineTicketHolder)
            _backgroundJobs.EnqueueLogUserBehaviour(
                _currentUser.UserId, livestream.LoungeShowId, BehaviourAction.WatchLivestream);

        return new LivestreamDetailDto(
            livestream.Id,
            livestream.LoungeShowId,
            livestream.LoungeShow.Name,
            livestream.Status,
            userHasAccess ? livestream.HlsUrl : null,
            livestream.ViewerCount,
            livestream.StartedAt,
            livestream.EndedAt,
            livestream.TerminatedReason,
            userHasAccess);
    }
}
