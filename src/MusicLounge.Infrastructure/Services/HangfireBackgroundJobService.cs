using Hangfire;
using MusicLounge.Application.Auth.Jobs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Livestreams.Jobs;
using MusicLounge.Application.LoungeShows.Commands.LogUserBehaviour;
using MusicLounge.Application.Tickets.Commands.CheckInLivestreamViewer;
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

    public void EnqueueLivestreamCheckIn(int userId, int showId)
        => BackgroundJob.Enqueue<CheckInLivestreamViewerJob>(
            j => j.ExecuteAsync(userId, showId));

    public void EnqueueLivestreamReconnectTimeout(int livestreamId, DateTimeOffset disconnectedAt, TimeSpan delay)
        => BackgroundJob.Schedule<LivestreamReconnectTimeoutJob>(
            j => j.ExecuteAsync(livestreamId, disconnectedAt), delay);

    public void EnqueueFcmNotification(
        int userId, string title, string body, string? referenceType = null, string? referenceId = null)
    {
        var data = new Dictionary<string, string>();
        if (referenceType is not null) data["referenceType"] = referenceType;
        if (referenceId is not null) data["referenceId"] = referenceId;

        BackgroundJob.Enqueue<IFcmService>(
            f => f.SendAsync(userId, title, body, data, CancellationToken.None));
    }

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

    public void EnqueuePhoneVerificationCode(string toPhone, string code)
    {
        var protectedCode = _secretProtector.Protect(code);
        BackgroundJob.Enqueue<SendPhoneVerificationCodeJob>(
            j => j.ExecuteAsync(toPhone, protectedCode, CancellationToken.None));
    }

    public void EnqueueModerationAiScoring(int moderationId)
        => BackgroundJob.Enqueue<ScoreModerationWithAiJob>(
            j => j.ExecuteAsync(moderationId, JobCancellationToken.Null));

    public void EnqueueStitchVenueTourScene(int attemptId, int loungeId, IReadOnlyList<string> sourceImageUrls, string? name)
        => BackgroundJob.Enqueue<StitchVenueTourSceneJob>(
            j => j.ExecuteAsync(attemptId, loungeId, sourceImageUrls, name, JobCancellationToken.Null));

    public void TriggerRecurringJobNow(string recurringJobId)
        => RecurringJob.TriggerJob(recurringJobId);
}
