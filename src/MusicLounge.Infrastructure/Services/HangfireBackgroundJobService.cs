using Hangfire;
using MusicLounge.Application.Auth.Jobs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;

namespace MusicLounge.Infrastructure.Services;

internal sealed class HangfireBackgroundJobService : IBackgroundJobService
{
    private readonly ISecretProtector _secretProtector;

    public HangfireBackgroundJobService(ISecretProtector secretProtector)
        => _secretProtector = secretProtector;

    public void EnqueueLogUserBehaviour(int userId, int showId, BehaviourAction action)
        => BackgroundJob.Enqueue<LogUserBehaviourJob>(
            j => j.ExecuteAsync(userId, showId, action));

    public void EnqueueRecommendationRefresh(int userId)
        => BackgroundJob.Enqueue<RefreshUserRecommendationJob>(
            j => j.ExecuteAsync(userId, JobCancellationToken.Null));

    public void EnqueueFcmNotification(int userId, string title, string body)
        => BackgroundJob.Enqueue<IFcmService>(
            f => f.SendAsync(userId, title, body, CancellationToken.None));

    public void EnqueuePasswordResetEmail(string toEmail, string toName, string resetLink)
    {
        var protectedLink = _secretProtector.Protect(resetLink);
        BackgroundJob.Enqueue<SendPasswordResetEmailJob>(
            j => j.ExecuteAsync(toEmail, toName, protectedLink, CancellationToken.None));
    }

    public void EnqueueEmailVerificationCode(string toEmail, string toName, string code)
    {
        var protectedCode = _secretProtector.Protect(code);
        BackgroundJob.Enqueue<SendEmailVerificationCodeJob>(
            j => j.ExecuteAsync(toEmail, toName, protectedCode, CancellationToken.None));
    }

    public void TriggerRecurringJobNow(string recurringJobId)
        => RecurringJob.TriggerJob(recurringJobId);
}
