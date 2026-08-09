using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueLogUserBehaviour(int userId, int showId, BehaviourAction action);
    void EnqueueRecommendationRefresh(int userId);
    void EnqueueFcmNotification(int userId, string title, string body);
    void EnqueuePasswordResetEmail(string toEmail, string toName, string resetLink);
    void EnqueueEmailVerificationCode(string toEmail, string toName, string code);
    void EnqueuePhoneVerificationCode(string toPhone, string code);

    // Runs AI moderation scoring for a freshly-created EventModeration row in the background, so a
    // slow/unavailable AI vendor never delays the Publish/CreateLivestream response it's called from.
    void EnqueueModerationAiScoring(int moderationId);

    // Cho Admin ep chay ngay 1 recurring job da dang ky (vd de kiem tra/van hanh), khong doi lich Cron.
    void TriggerRecurringJobNow(string recurringJobId);
}
