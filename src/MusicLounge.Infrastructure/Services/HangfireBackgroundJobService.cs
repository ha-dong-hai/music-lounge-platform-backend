using Hangfire;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Infrastructure.Services;

internal sealed class HangfireBackgroundJobService : IBackgroundJobService
{
    public void EnqueueLogUserBehaviour(int userId, int showId, BehaviourAction action)
        => BackgroundJob.Enqueue<LogUserBehaviourJob>(
            j => j.ExecuteAsync(userId, showId, action));

    public void EnqueueRecommendationRefresh(int userId)
        => BackgroundJob.Enqueue<IAIRecommendationService>(
            s => s.TriggerRecommendationRefreshAsync(userId, CancellationToken.None));

    public void EnqueueFcmNotification(int userId, string title, string body)
        => BackgroundJob.Enqueue<IFcmService>(
            f => f.SendAsync(userId, title, body, CancellationToken.None));

    public void EnqueuePasswordResetEmail(string toEmail, string toName, string resetLink)
        => BackgroundJob.Enqueue<IEmailService>(
            e => e.SendPasswordResetEmailAsync(toEmail, toName, resetLink, CancellationToken.None));

    public void EnqueueEmailVerificationCode(string toEmail, string toName, string code)
        => BackgroundJob.Enqueue<IEmailService>(
            e => e.SendEmailVerificationCodeAsync(toEmail, toName, code, CancellationToken.None));

    public void TriggerRecurringJobNow(string recurringJobId)
        => RecurringJob.TriggerJob(recurringJobId);
}
