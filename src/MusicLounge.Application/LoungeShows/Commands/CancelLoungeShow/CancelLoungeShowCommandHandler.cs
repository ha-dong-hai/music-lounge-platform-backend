using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

// Huy ca show (nang hon ChangeLoungeShowFormat - show khong con dien ra nua) nen phai it nhat
// hao phong bang: hoan 100% CHO MOI ve Confirmed (khong chi rieng Physical nhu doi format,
// vi ca ve Livestream cung mat gia tri khi show bi huy hoan toan) + notify tung ticket holder.
internal sealed class CancelLoungeShowCommandHandler : IRequestHandler<CancelLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly IAsyncKeyedLock _lock;

    public CancelLoungeShowCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILivestreamRepository livestreamRepo, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _livestreamRepo = livestreamRepo;
        _lock = @lock;
    }

    public async Task<Unit> Handle(CancelLoungeShowCommand request, CancellationToken ct)
    {
        // Same key namespace as ChangeLoungeShowFormatCommandHandler — serializes Cancel against a
        // concurrent format-change on the same show too, not just against a double-click of Cancel
        // itself, since both create RefundRequest rows off the same ticket set.
        await using var _ = await _lock.AcquireAsync($"show-status-change:{request.ShowId}", ct);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền hủy event này.");

        if (show.Status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended)
            throw new DomainException("Event đã kết thúc hoặc đã bị hủy trước đó.");

        // Unlike StartLoungeShowCommandHandler (blocks if a livestream row exists at all), a show
        // with a livestream tier that's merely Scheduled/Ended/Terminated is fine to cancel — the
        // one state that must block is Live: cancelling+refunding while it's actively broadcasting
        // to paying viewers would leave the stream running with nothing telling it to stop.
        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream?.Status == LivestreamStatus.Live)
            throw new DomainException(
                "Show đang phát trực tiếp — hãy dừng (terminate) livestream trước khi hủy event.");

        // MLACP-373: phan huy — hoan 100% moi ve, bao nguoi giu ve — nam o ShowCancellation, dung chung voi job ap an
        // phat khi phong tra bi khoa / tam khoa.
        await ShowCancellation.CancelAsync(_uow, _notifications, show, why: null, ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
