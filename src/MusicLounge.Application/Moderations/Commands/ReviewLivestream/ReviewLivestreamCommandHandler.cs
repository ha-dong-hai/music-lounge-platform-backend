using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Moderations.Commands.ReviewLivestream;

internal sealed class ReviewLivestreamCommandHandler : IRequestHandler<ReviewLivestreamCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly INotificationService _notifications;

    public ReviewLivestreamCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IEventModerationRepository moderationRepo,
        INotificationService notifications)
    {
        _uow = uow;
        _currentUser = currentUser;
        _moderationRepo = moderationRepo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ReviewLivestreamCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision == ModerationDecision.Terminated)
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        var moderation = await _moderationRepo.GetByTargetAsync(
            ModerationTargetType.Livestream, request.LivestreamId, ct)
            ?? throw new NotFoundException("EventModeration for Livestream", request.LivestreamId);

        if (moderation.AdminDecision is not null)
            throw new ConflictException("Livestream này đã được duyệt trước đó.");

        if (livestream.Status is LivestreamStatus.Live
                              or LivestreamStatus.Ended
                              or LivestreamStatus.Terminated)
            throw new ConflictException("Không thể thay đổi quyết định khi livestream đã/đang phát sóng.");

        moderation.AdminDecision = decision;
        moderation.AdminId = _currentUser.UserId;
        moderation.ReviewNote = request.ReviewNote;
        moderation.ReviewedAt = DateTimeOffset.UtcNow;

        _moderationRepo.Update(moderation);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct);
        var lounge = show is null
            ? null
            : await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct);
        if (lounge is not null)
        {
            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.ModerationResult,
                decision == ModerationDecision.Approved ? "Livestream đã được duyệt" : "Livestream bị từ chối",
                decision == ModerationDecision.Approved
                    ? $"Livestream cho \"{show!.Name}\" đã được duyệt, có thể bắt đầu phát sóng."
                    : $"Livestream cho \"{show!.Name}\" bị từ chối. Lý do: {request.ReviewNote ?? "không có ghi chú"}.",
                referenceType: "livestream",
                referenceId: livestream.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
