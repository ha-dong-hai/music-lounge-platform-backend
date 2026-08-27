using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueLogUserBehaviour(int userId, int showId, BehaviourAction action);
    void EnqueueRecommendationRefresh(int userId);

    // MLACP-140: "check-in" cho ve Livestream — khong co quay/nhan vien quet QR nhu ve vat ly
    // (CheckInTicketCommandHandler chi ap dung AccessType.Physical), nen viec thuc su nhan duoc
    // HlsUrl phat (chi xay ra khi da xac minh la chu ve that qua HasViewerAccessAsync — xem
    // GetLivestreamDetailQueryHandler) la bang chung "da tham du" tuong duong. Job chuyen cac ve
    // Livestream Confirmed cua user+show nay sang Used, de RateShowCommandHandler dung chung 1
    // dieu kien Status=Used cho ca 2 loai ve thay vi phai mien check-in rieng cho ve online.
    void EnqueueLivestreamCheckIn(int userId, int showId);
    void EnqueueFcmNotification(int userId, string title, string body);
    void EnqueuePasswordResetEmail(string toEmail, string toName, string resetLink);
    void EnqueueEmailVerificationCode(string toEmail, string toName, string code);
    void EnqueuePhoneVerificationCode(string toPhone, string code);

    // Runs AI moderation scoring for a freshly-created EventModeration row in the background, so a
    // slow/unavailable AI vendor never delays the Publish/CreateLivestream response it's called from.
    void EnqueueModerationAiScoring(int moderationId);

    // Panorama stitching can take 15-30+ seconds (sometimes brushing the panorama-stitcher
    // HttpClient's 120s timeout on harder photo sets) - running it inline would block the Owner's
    // HTTP request for that whole window. The attempt row (already Pending when this is called)
    // is updated to Succeeded/Failed by the job itself; the Owner polls for the result instead.
    void EnqueueStitchVenueTourScene(int attemptId, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name);

    // Cho Admin ep chay ngay 1 recurring job da dang ky (vd de kiem tra/van hanh), khong doi lich Cron.
    void TriggerRecurringJobNow(string recurringJobId);
}
