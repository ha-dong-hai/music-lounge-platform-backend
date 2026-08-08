using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamDetail;

internal sealed class GetLivestreamDetailQueryHandler : IRequestHandler<GetLivestreamDetailQuery, LivestreamDetailDto>
{
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ICurrentUserService _currentUser;

    public GetLivestreamDetailQueryHandler(
        ILivestreamRepository livestreamRepo,
        ICurrentUserService currentUser)
    {
        _livestreamRepo = livestreamRepo;
        _currentUser = currentUser;
    }

    public async Task<LivestreamDetailDto> Handle(GetLivestreamDetailQuery request, CancellationToken ct)
    {
        var livestream = await _livestreamRepo.GetByIdWithDetailsAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException("Livestream", request.LivestreamId);

        // HLS URL is only visible to users with a valid livestream ticket,
        // or to Staff/Admin who need to monitor the stream.
        var userHasAccess = _currentUser.Role is Roles.Admin or Roles.Staff
            || (_currentUser.IsAuthenticated
                && await _livestreamRepo.HasViewerAccessAsync(request.LivestreamId, _currentUser.UserId, ct));

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
