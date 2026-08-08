using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Moderations.Commands.ReviewShow;

internal sealed class ReviewShowCommandHandler : IRequestHandler<ReviewShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly IFollowRepository _followRepo;
    private readonly INotificationService _notifications;

    public ReviewShowCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IEventModerationRepository moderationRepo,
        IFollowRepository followRepo,
        INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _moderationRepo = moderationRepo;
        _followRepo = followRepo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ReviewShowCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision == ModerationDecision.Terminated)
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

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

        // Approved → Published (visible/purchasable). Rejected → back to Draft so Owner can fix & resubmit.
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

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct);
        if (lounge is not null)
        {
            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.ModerationResult,
                decision == ModerationDecision.Approved ? "Chương trình đã được duyệt" : "Chương trình bị từ chối",
                decision == ModerationDecision.Approved
                    ? $"\"{show.Name}\" đã được duyệt và xuất bản."
                    : $"\"{show.Name}\" bị từ chối duyệt. Lý do: {request.ReviewNote ?? "không có ghi chú"}.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);

            if (decision == ModerationDecision.Approved)
            {
                var followerIds = await _followRepo.GetFollowerUserIdsAsync(show.LoungeId, ct);
                foreach (var userId in followerIds)
                {
                    await _notifications.NotifyAsync(
                        userId,
                        NotificationType.NewEvent,
                        "Chương trình mới",
                        $"{lounge.Name} vừa đăng chương trình mới: \"{show.Name}\".",
                        referenceType: "show",
                        referenceId: show.Id.ToString(),
                        ct: ct);
                }
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
