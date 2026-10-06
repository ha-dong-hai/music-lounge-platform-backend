using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Lounges;
using MusicLounge.Application.Notifications;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Moderations.Commands.ReviewLoungeMedia;

/// <summary>
/// MLACP-692. Ảnh thư viện và cảnh tour 360 bị AI chấm vào vùng "cần người xem" (ImageModerationGate: chặn ≥85%, xem ≥50%)
/// được đăng NGAY nhưng kèm một bản ghi EventModeration chờ Admin. Trước MLACP-692 không có API nào xử lý hai loại này:
/// bản ghi nằm "chờ duyệt" mãi, đếm vào số "Chờ duyệt" của trang Admin (Azure 06/10: tổng 3 trong khi các tab cộng lại 2),
/// và Admin không có cách nào gỡ một ảnh không phù hợp — lời hứa "AI gắn cờ, người duyệt" không có cơ chế.
///
/// Approved: giữ nội dung, đóng bản ghi. Rejected (bắt buộc lý do): GỠ nội dung bằng đúng cách chủ phòng trà tự xoá
/// (LoungeMediaRemoval), đóng bản ghi, báo chủ phòng trà kèm lý do. Nội dung đã bị chủ phòng trà xoá trước khi duyệt thì
/// vẫn đóng được bản ghi (không còn gì để gỡ) — nếu không, bản ghi đó sẽ kẹt trong hàng chờ mãi.
/// </summary>
internal sealed class ReviewLoungeMediaCommandHandler : IRequestHandler<ReviewLoungeMediaCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;

    public ReviewLoungeMediaCommandHandler(
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

    public async Task<Unit> Handle(ReviewLoungeMediaCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationTargetType>(request.TargetType, true, out var loai)
            || loai is not (ModerationTargetType.GalleryImage or ModerationTargetType.TourScene))
            throw new DomainException("Loại nội dung phải là 'GalleryImage' hoặc 'TourScene'.");
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision == ModerationDecision.Terminated)
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

        // Cùng lý do với ReviewTicketTierCommandHandler: hai Admin duyệt cùng lúc thì một quyết định ghi đè mất quyết định kia.
        await using var _ = await _lock.AcquireAsync($"moderation:{loai}:{request.TargetId}", ct);

        var moderation = await _moderationRepo.GetByTargetAsync(loai, request.TargetId, ct)
            ?? throw new NotFoundException($"EventModeration for {loai}", request.TargetId);
        if (moderation.AdminDecision is not null)
            throw new ConflictException("Nội dung này đã được duyệt trước đó.");

        // Tìm nội dung + phòng trà của nó (có thể đã bị chủ phòng trà xoá trước khi Admin xem).
        MusicLoungeEntity? lounge = null;
        LoungeGalleryImage? anh = null;
        VenueTourScene? canh = null;
        if (loai == ModerationTargetType.GalleryImage)
        {
            anh = await _uow.Repository<LoungeGalleryImage, Guid>().GetByIdAsync(request.TargetId, ct);
            if (anh is not null) lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(anh.LoungeId, ct);
        }
        else
        {
            canh = await _uow.Repository<VenueTourScene, Guid>().GetByIdAsync(request.TargetId, ct);
            if (canh is not null) lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(canh.LoungeId, ct);
        }

        moderation.AdminDecision = decision;
        moderation.AdminId = _currentUser.UserId;
        moderation.ReviewNote = request.ReviewNote;
        moderation.ReviewedAt = DateTimeOffset.UtcNow;
        _moderationRepo.Update(moderation);

        if (decision == ModerationDecision.Rejected && lounge is not null)
        {
            if (anh is not null) await LoungeMediaRemoval.RemoveGalleryImageAsync(_uow, lounge, anh, ct);
            if (canh is not null) await LoungeMediaRemoval.RemoveTourSceneAsync(_uow, canh, ct);
        }

        if (lounge is not null)
        {
            var (vi, en) = loai == ModerationTargetType.GalleryImage
                ? ("Ảnh thư viện", "Gallery photo")
                : (canh?.Name is { Length: > 0 } ten ? $"Cảnh 360° \"{ten}\"" : "Cảnh 360°", "360° scene");
            await _notifications.NotifyAsync(
                lounge.OwnerId,
                NotificationType.ModerationResult,
                new SongNgu(
                    decision == ModerationDecision.Approved ? $"{vi} đã được duyệt" : $"{vi} đã bị gỡ",
                    decision == ModerationDecision.Approved ? $"{en} approved" : $"{en} removed"),
                new SongNgu(
                    decision == ModerationDecision.Approved
                        ? $"{vi} của \"{lounge.Name}\" đã được quản trị viên xem và giữ nguyên."
                        : $"{vi} của \"{lounge.Name}\" đã bị gỡ khỏi trang phòng trà. Lý do: {request.ReviewNote}",
                    decision == ModerationDecision.Approved
                        ? $"The {en.ToLowerInvariant()} of \"{lounge.Name}\" was reviewed and kept."
                        : $"The {en.ToLowerInvariant()} of \"{lounge.Name}\" was removed from the venue page. Reason: {request.ReviewNote}"),
                referenceType: NotificationReferenceTypes.Lounge,
                referenceId: lounge.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
