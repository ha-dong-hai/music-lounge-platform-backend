using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;

internal sealed class SetPlaybackModeCommandHandler : IRequestHandler<SetPlaybackModeCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SetPlaybackModeCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(SetPlaybackModeCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền sửa event này.");

        if (show.Status is LoungeShowStatus.Ended or LoungeShowStatus.Cancelled)
            throw new DomainException("Không thể đổi hình thức phát cho show đã kết thúc/hủy.");

        var mode = Enum.Parse<LivestreamPlaybackMode>(request.PlaybackMode);

        if (mode == LivestreamPlaybackMode.ThreeD && show.Format == LoungeShowFormat.Offline)
            throw new DomainException("Chỉ show Online hoặc Hybrid mới có thể phát dạng 3D.");

        show.PlaybackMode = mode;
        showRepo.Update(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
