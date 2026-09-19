using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Notifications;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.PosterJobs.Commands.FailPosterJob;

/// <summary>
/// MLACP-458. Đóng đơn khi máy trạm báo hỏng.
///
/// KHÔNG tự thử lại ở đây: máy trạm chỉ báo hỏng khi chính Google Flow từ chối (hết hạn mức, lời nhắc bị chặn), tức là
/// thử lại ngay cũng hỏng y như vậy và chỉ tốn thêm một lượt hạn mức. Thử lại chỉ dành cho trường hợp máy trạm im lặng
/// — do <c>ExpirePosterJobsJob</c> lo, vì đó mới là lỗi tạm thời.
///
/// Đơn hỏng chuyển <c>Failed</c> nên không còn bị đếm vào hạn mức tháng nữa — chủ phòng trà được trả lại lượt, đúng
/// nguyên tắc của MLACP-419: lỗi của nhà cung cấp thì không tính vào tiền người ta đã trả.
/// </summary>
internal sealed class FailPosterJobCommandHandler : IRequestHandler<FailPosterJobCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;

    public FailPosterJobCommandHandler(IUnitOfWork uow, INotificationService notifications)
    {
        _uow = uow;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(FailPosterJobCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<AiPosterGeneration, int>();
        var job = await repo.GetByIdAsync(request.JobId, ct)
            ?? throw new NotFoundException(nameof(AiPosterGeneration), request.JobId);

        if (job.Status != AiPosterGenerationStatus.Rendering || job.ClaimedBy != request.WorkerId)
            throw new ConflictException(
                "Đơn tạo poster này không còn do máy trạm của bạn giữ (có thể đã hết hạn và được giao lại).");

        job.Status = AiPosterGenerationStatus.Failed;
        job.ErrorMessage = request.Reason;
        job.LeaseExpiresAt = null;
        repo.Update(job);

        // Câu cho người dùng KHÔNG chứa lý do kỹ thuật máy trạm gửi lên: nó là thông điệp của nhà cung cấp (thường tiếng
        // Anh, đôi khi kèm mã nội bộ). Lý do gốc vẫn nằm trong ErrorMessage cho người vận hành đối chiếu.
        await _notifications.NotifyAsync(
            job.OwnerId,
            NotificationType.PosterGenerationResult,
            "Chưa tạo được poster",
            "Hệ thống chưa tạo được poster cho buổi hòa nhạc của bạn. Bạn không bị trừ lượt poster nào — " +
            "vui lòng thử lại, hoặc tự tải poster của bạn lên.",
            NotificationReferenceTypes.Show,
            job.ShowId.ToString(),
            ct);

        // NotifyAsync chỉ xếp hàng bản ghi; SaveChangesAsync ở đây ghi cả trạng thái đơn lẫn thông báo trong một lượt.
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
