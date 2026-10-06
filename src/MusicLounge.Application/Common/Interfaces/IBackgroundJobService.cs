using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;

namespace MusicLounge.Application.Common.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueLogUserBehaviour(Guid userId, Guid showId, BehaviourAction action);
    void EnqueueRecommendationRefresh(Guid userId);

    // MLACP-140: "check-in" cho ve Livestream — khong co quay/nhan vien quet QR nhu ve vat ly
    // (CheckInTicketCommandHandler chi ap dung AccessType.Physical), nen viec thuc su nhan duoc
    // HlsUrl phat (chi xay ra khi da xac minh la chu ve that qua HasViewerAccessAsync — xem
    // GetLivestreamDetailQueryHandler) la bang chung "da tham du" tuong duong. Job chuyen cac ve
    // Livestream Confirmed cua user+show nay sang Used, de RateShowCommandHandler dung chung 1
    // dieu kien Status=Used cho ca 2 loai ve thay vi phai mien check-in rieng cho ve online.
    void EnqueueLivestreamCheckIn(Guid userId, Guid showId);

    // MLACP-191: len lich kiem tra sau `delay` (system_config: livestream_reconnect_timeout_minutes)
    // xem livestream con dang Reconnecting voi dung DisconnectedAt da ghi nhan luc enqueue khong —
    // neu con thi danh dau Failed. disconnectedAt lam guard chong job cu bi tre sau 1 chu ky ngat/
    // ket noi lai khac da xay ra.
    void EnqueueLivestreamReconnectTimeout(Guid livestreamId, DateTimeOffset disconnectedAt, TimeSpan delay);
    void EnqueueFcmNotification(
        Guid userId, string title, string body, string? referenceType = null, string? referenceId = null);
    // MLACP-489: language = User.PreferredLanguage của người nhận — ba thứ này gửi bất đồng bộ, lúc gửi không có
    // request nào để đọc Accept-Language.
    void EnqueuePasswordResetEmail(string toEmail, string toName, string resetLink, string language);
    void EnqueueEmailVerificationCode(string toEmail, string toName, string code, string language);

    /// <summary>MLACP-642. Xếp hàng thư mời nghệ sĩ tự xác nhận — gửi ngoài giao dịch của lệnh tạo ra nó
    /// (xem <c>SendPerformerConfirmationEmailJob</c>). Liên kết được mã hoá trước khi vào kho job.
    /// MLACP-673: evidenceUrl = ảnh chứng từ phòng trà đã nộp — job đọc ảnh lúc gửi và đính kèm vào thư.</summary>
    void EnqueuePerformerConfirmationEmail(
        string toEmail, string toName, SongNgu subject, SongNgu message, string link, DateTimeOffset expiresAt,
        string? evidenceUrl = null);
    void EnqueuePhoneVerificationCode(string toPhone, string code, string language);

    // Runs AI moderation scoring for a freshly-created EventModeration row in the background, so a
    // slow/unavailable AI vendor never delays the Publish/CreateLivestream response it's called from.
    void EnqueueModerationAiScoring(Guid moderationId);

    /// <summary>MLACP-574: AI chấm lời bình của một đánh giá vừa gửi (ScoreRatingWithAiJob) — rủi ro cao thì ẩn tạm,
    /// chờ Admin quyết. Chạy nền để AI chậm/hỏng không ảnh hưởng việc gửi đánh giá.</summary>
    void EnqueueRatingAiScoring(Guid ratingId);

    /// <summary>MLACP-635: thư xác nhận vé cho một lần thanh toán online (SendTicketConfirmationEmailJob). Chạy nền: gửi thư
    /// hỏng hay chậm không được làm hỏng luồng thanh toán, và Hangfire thử lại khi SMTP lỗi tạm thời.</summary>
    void EnqueueTicketConfirmationEmail(Guid paymentId);

    // Panorama stitching can take 15-30+ seconds (sometimes brushing the panorama-stitcher
    // HttpClient's 120s timeout on harder photo sets) - running it inline would block the Owner's
    // HTTP request for that whole window. The attempt row (already Pending when this is called)
    // is updated to Succeeded/Failed by the job itself; the Owner polls for the result instead.
    void EnqueueStitchVenueTourScene(Guid attemptId, Guid loungeId, IReadOnlyList<string> sourceImageUrls, string? name);

    // Cho Admin ep chay ngay 1 recurring job da dang ky (vd de kiem tra/van hanh), khong doi lich Cron.
    void TriggerRecurringJobNow(string recurringJobId);

    /// <summary>
    /// The recurring job ids actually registered at startup. Read rather than remembered, because
    /// Hangfire no-ops silently on an unknown id — a stale list would show a job as triggerable and
    /// then quietly do nothing.
    /// </summary>
    IReadOnlyList<string> GetRecurringJobIds();
}
