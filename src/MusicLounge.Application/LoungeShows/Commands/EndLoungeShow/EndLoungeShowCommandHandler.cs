using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.EndLoungeShow;

// Doi ung voi StartLoungeShowCommand - danh cho show KHONG co Livestream. Set RatingOpenUntil
// (§6.13) giong het EndLivestreamCommandHandler/TerminateLivestreamCommandHandler.
internal sealed class EndLoungeShowCommandHandler : IRequestHandler<EndLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ISystemConfigService _config;

    public EndLoungeShowCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, ILivestreamRepository livestreamRepo,
        ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _livestreamRepo = livestreamRepo;
        _config = config;
    }

    public async Task<Unit> Handle(EndLoungeShowCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền kết thúc show của venue này.");

        if (show.Status != LoungeShowStatus.Ongoing)
            throw new DomainException("Chỉ có thể kết thúc show đang diễn ra (Ongoing).");

        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream is not null)
            throw new DomainException(
                "Show này có livestream — dùng chức năng kết thúc livestream thay vì lệnh này.");

        var now = DateTimeOffset.UtcNow;
        var ratingWindowDays = await _config.GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);
        show.Status = LoungeShowStatus.Ended;
        show.ActualEnd = now;
        show.RatingOpenUntil = now.AddDays(ratingWindowDays);   // §6.13
        showRepo.Update(show);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
