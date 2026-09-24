using MusicLounge.Domain.ValueObjects;
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
    private readonly IAsyncKeyedLock _lock;

    public ReviewLivestreamCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IEventModerationRepository moderationRepo,
        INotificationService notifications,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _moderationRepo = moderationRepo;
        _notifications = notifications;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ReviewLivestreamCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision == ModerationDecision.Terminated)
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

        // Same double-review race as ReviewShowCommandHandler — see its comment.
        await using var _ = await _lock.AcquireAsync($"moderation:livestream:{request.LivestreamId}", ct);

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
                new SongNgu(
                    decision == ModerationDecision.Approved ? "Livestream đã được duyệt" : "Livestream bị từ chối",
                    decision == ModerationDecision.Approved ? "Livestream approved" : "Livestream rejected"),
                new SongNgu(
                    decision == ModerationDecision.Approved
                        ? $"Livestream cho \"{show!.Name}\" đã được duyệt, có thể bắt đầu phát sóng."
                        : $"Livestream cho \"{show!.Name}\" bị từ chối. Lý do: {request.ReviewNote ?? "không có ghi chú"}.",
                    decision == ModerationDecision.Approved
                        ? $"The livestream for \"{show!.Name}\" has been approved and can start broadcasting."
                        : $"The livestream for \"{show!.Name}\" was rejected. Reason: {request.ReviewNote ?? "no note"}."),
                referenceType: "livestream",
                // MLACP-460: mã BUỔI HÒA NHẠC, không phải mã buổi phát. Frontend bấm vào thông báo là mở trang buổi hòa
                // nhạc — đường dẫn đó nhận mã show. Trước đây trả mã livestream nên hoặc mở nhầm buổi khác (hai mã trùng
                // số), hoặc ra trang trống. Loại tham chiếu vẫn là "livestream" để frontend biết đây là kết quả duyệt
                // buổi phát chứ không phải duyệt nội dung buổi hòa nhạc.
                referenceId: show!.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
