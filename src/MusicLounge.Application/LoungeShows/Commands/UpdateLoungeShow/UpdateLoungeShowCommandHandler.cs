using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.UpdateLoungeShow;

internal sealed class UpdateLoungeShowCommandHandler : IRequestHandler<UpdateLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public UpdateLoungeShowCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(UpdateLoungeShowCommand request, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Bạn không có quyền sửa event này.");

        if (show.Status != LoungeShowStatus.Draft)
            throw new DomainException("Chỉ có thể sửa event khi còn ở trạng thái Draft.");

        show.Name = request.Name;
        show.Description = request.Description;
        show.ScheduledStart = request.ScheduledStart;
        show.ScheduledEnd = request.ScheduledEnd;
        show.CategoryId = request.CategoryId;
        show.OfflineQuota = request.OfflineQuota;
        show.OnlineQuota = request.OnlineQuota;

        showRepo.Update(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
