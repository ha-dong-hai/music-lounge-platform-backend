using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Queries.GetLivestreamCredentials;

internal sealed class GetLivestreamCredentialsQueryHandler
    : IRequestHandler<GetLivestreamCredentialsQuery, LivestreamCredentialsDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetLivestreamCredentialsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<LivestreamCredentialsDto> Handle(GetLivestreamCredentialsQuery request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        if (_currentUser.Role != Roles.Admin)
        {
            var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct)
                ?? throw new NotFoundException(nameof(LoungeShow), livestream.LoungeShowId);

            // D6: giong het scoping cua Create livestream — Staff cua dung venue hoac chinh
            // Owner cua venue do moi xem duoc credential.
            var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
                ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
            if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
                throw new ForbiddenException("Bạn không có quyền xem thông tin phát trực tiếp của venue này.");
        }

        return new LivestreamCredentialsDto(
            livestream.Id, livestream.Provider, livestream.RtmpUrl, livestream.StreamKey);
    }
}
