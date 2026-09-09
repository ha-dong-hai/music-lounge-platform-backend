using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Admin.Commands.ReviewVenue;

/// <summary>
/// BR-01. Bước quyết định mà cả hệ thống vốn giả định là đã có: entity mặc định
/// <see cref="LoungeStatus.Pending"/>, doc comment của POST /lounges ghi "chờ Admin duyệt", và tài
/// liệu BA mô tả luồng này như thể nó đang chạy — nhưng không có một endpoint nào duyệt được, nên
/// hồ sơ nộp lên rồi nằm ở Pending vĩnh viễn. Vì trạng thái đó không chặn gì cả, hệ quả là phòng
/// trà chưa ai xác minh vẫn hiện công khai và vẫn bán vé thu tiền thật.
///
/// Không đụng tới các trạng thái do án phạt quản (Suspended/Locked/Warned). Nếu endpoint này duyệt
/// được một venue đang bị đình chỉ thành Approved thì nó trở thành đường vòng gỡ án phạt, bỏ qua
/// cả luồng khiếu nại lẫn ExpireServedSuspensionsJob.
/// </summary>
internal sealed class ReviewVenueCommandHandler : IRequestHandler<ReviewVenueCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAsyncKeyedLock _lock;

    public ReviewVenueCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        INotificationService notifications,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ReviewVenueCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<ModerationDecision>(request.Decision, true, out var decision)
            || decision is not (ModerationDecision.Approved or ModerationDecision.Rejected))
            throw new DomainException("Quyết định không hợp lệ. Dùng 'Approved' hoặc 'Rejected'.");

        // Hai Admin cùng mở một hồ sơ trong hàng đợi là chuyện bình thường; không có khoá thì cả
        // hai đều đọc được trạng thái Pending trước khi bên nào kịp ghi, và quyết định sau đè lên
        // quyết định trước mà không ai biết.
        await using var _ = await _lock.AcquireAsync($"venue-review:{request.LoungeId}", ct);

        var repo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await repo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        if (lounge.Status is LoungeStatus.Suspended or LoungeStatus.Locked or LoungeStatus.Warned)
            throw new ConflictException(
                $"Phòng trà đang ở trạng thái '{lounge.Status}' do án phạt — trạng thái này thuộc " +
                "luồng xử lý vi phạm, không gỡ được bằng bước duyệt hồ sơ.");

        if (lounge.Status == LoungeStatus.Approved)
            throw new ConflictException("Phòng trà này đã được duyệt trước đó.");

        if (lounge.Status == LoungeStatus.Rejected && decision == ModerationDecision.Rejected)
            throw new ConflictException("Hồ sơ phòng trà này đã bị từ chối trước đó.");

        var approved = decision == ModerationDecision.Approved;

        lounge.Status = approved ? LoungeStatus.Approved : LoungeStatus.Rejected;
        lounge.StatusReviewedAt = DateTimeOffset.UtcNow;
        lounge.StatusReviewedBy = _currentUser.UserId;
        lounge.StatusReviewNote = request.ReviewNote;
        repo.Update(lounge);

        // Chỉ dàn hàng, chưa ghi — NotificationService theo đúng giao kèo cũ, lời gọi
        // SaveChangesAsync duy nhất bên dưới mới là cái ghi cả quyết định lẫn thông báo về nó.
        await _notifications.NotifyAsync(
            lounge.OwnerId,
            NotificationType.VenueReviewResult,
            approved ? "Phòng trà đã được duyệt" : "Hồ sơ phòng trà bị từ chối",
            approved
                ? $"\"{lounge.Name}\" đã được duyệt. Bạn có thể bắt đầu tạo và nộp duyệt buổi diễn."
                : $"\"{lounge.Name}\" chưa được duyệt. Lý do: {request.ReviewNote} " +
                  "Bạn có thể chỉnh sửa hồ sơ và liên hệ Admin để được xem xét lại.",
            referenceType: "lounge",
            referenceId: lounge.Id.ToString(),
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
