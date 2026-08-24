using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Moderations.Commands.ReviewShow;

internal sealed class ReviewShowCommandHandler : IRequestHandler<ReviewShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly IAsyncKeyedLock _lock;

    public ReviewShowCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IEventModerationRepository moderationRepo,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _moderationRepo = moderationRepo;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ReviewShowCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision == ModerationDecision.Terminated)
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

        // Two Admins reviewing the same pending show within the same instant is the failure this
        // guards against: without a lock, both can read AdminDecision == null before either
        // commits, and both approve/reject — one decision silently overwrites the other's.
        await using var _ = await _lock.AcquireAsync($"moderation:show:{request.ShowId}", ct);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var moderation = await _moderationRepo.GetByTargetAsync(
            ModerationTargetType.Show, request.ShowId, ct)
            ?? throw new NotFoundException("EventModeration for Show", request.ShowId);

        if (moderation.AdminDecision is not null)
            throw new ConflictException("Event này đã được duyệt trước đó.");

        if (show.Status != LoungeShowStatus.Pending)
            throw new ConflictException("Chỉ có thể duyệt event đang chờ duyệt (Pending).");

        moderation.AdminDecision = decision;
        moderation.AdminId = _currentUser.UserId;
        moderation.ReviewNote = request.ReviewNote;
        moderation.ReviewedAt = DateTimeOffset.UtcNow;
        _moderationRepo.Update(moderation);

        // Approved → Published (visible/purchasable). Rejected → back to Draft so Owner can fix &
        // resubmit, thay vi Cancelled (ngo cut, Owner phai tao event moi tu dau).
        show.Status = decision == ModerationDecision.Approved
            ? LoungeShowStatus.Published
            : LoungeShowStatus.Draft;

        // D18: duyet noi dung dong thoi la buoc Admin xac nhan van ban chap thuan bieu dien
        // (LegalApprovalReference) Owner da khai bao la hop le — khong tach thanh quy trinh rieng.
        if (decision == ModerationDecision.Approved)
        {
            show.LegalApprovalConfirmedByAdminId = _currentUser.UserId;
            show.LegalApprovalConfirmedAt = DateTimeOffset.UtcNow;
        }

        showRepo.Update(show);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
