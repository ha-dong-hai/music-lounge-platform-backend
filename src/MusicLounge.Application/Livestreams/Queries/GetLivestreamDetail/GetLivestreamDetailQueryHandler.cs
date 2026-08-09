using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

internal sealed class GetLivestreamDetailQueryHandler : IRequestHandler<GetLivestreamDetailQuery, LivestreamDetailDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;

    public GetLivestreamDetailQueryHandler(
        IUnitOfWork uow,
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser)
    {
        _uow = uow;
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
    }

    public async Task<LivestreamDetailDto> Handle(GetLivestreamDetailQuery request, CancellationToken ct)
    {
        var livestream = await _livestreamRepo.GetByIdWithDetailsAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException("Livestream", request.LivestreamId);

        // HLS URL is only visible to users with a valid livestream ticket, or to Staff/Owner of
        // THIS venue who need to monitor the stream — was previously any Staff/Admin account
        // regardless of venue, letting Staff of venue A watch venue B's paid livestream for free.
        var userHasAccess = _currentUser.Role == Roles.Admin;
        if (!userHasAccess)
        {
            var lounge = await _uow.Repository<MusicLoungeEntity, int>()
                .GetByIdAsync(livestream.LoungeShow.LoungeId, ct);
            userHasAccess = (lounge is not null
                    && VenueOperatorAccess.CanOperate(_currentUser, livestream.LoungeShow.LoungeId, lounge.OwnerId))
                || (_currentUser.IsAuthenticated
                    && await _livestreamRepo.HasViewerAccessAsync(request.LivestreamId, _currentUser.UserId, ct));
        }

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
