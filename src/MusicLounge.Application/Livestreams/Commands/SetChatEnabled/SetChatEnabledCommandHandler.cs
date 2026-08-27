using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Commands.SetChatEnabled;

internal sealed class SetChatEnabledCommandHandler : IRequestHandler<SetChatEnabledCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetChatEnabledCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetChatEnabledCommand request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), livestream.LoungeShowId);
        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền bật/tắt chat cho livestream này.");

        livestream.ChatEnabled = request.Enabled;
        _uow.Repository<Livestream, int>().Update(livestream);
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
