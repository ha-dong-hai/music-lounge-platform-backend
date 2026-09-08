using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.SetPlaybackMode;

/// <summary>
/// LoungeShow.PlaybackMode được trả ra trong DTO chi tiết buổi diễn để client biết dựng trình phát
/// 2D hay 3D, nhưng không endpoint nào đặt được nó — mọi buổi diễn vì thế nằm im ở giá trị mặc định.
/// </summary>
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

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Bạn không có quyền sửa buổi hòa nhạc này.");

        // Cùng khái niệm trạng thái kết thúc mà LoungeShowLifecycle đang giữ cho toàn hệ thống:
        // buổi diễn đã hủy hoặc đã kết thúc thì không còn gì để đổi cách phát nữa.
        if (LoungeShowLifecycle.IsTerminal(show.Status))
            throw new DomainException(
                "Không thể đổi hình thức phát cho buổi hòa nhạc đã kết thúc hoặc đã hủy.");

        var mode = Enum.Parse<LivestreamPlaybackMode>(request.PlaybackMode, ignoreCase: true);

        // Buổi diễn Offline không có luồng phát nào để dựng 3D lên trên.
        if (mode == LivestreamPlaybackMode.ThreeD && show.Format == LoungeShowFormat.Offline)
            throw new DomainException(
                "Chỉ buổi hòa nhạc Online hoặc Hybrid mới phát được dạng 3D.");

        show.PlaybackMode = mode;
        showRepo.Update(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
