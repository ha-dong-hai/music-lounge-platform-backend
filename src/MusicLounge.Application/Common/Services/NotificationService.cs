using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common.Services;

internal sealed class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IBackgroundJobService _jobs;

    public NotificationService(IUnitOfWork uow, IBackgroundJobService jobs)
    {
        _uow = uow;
        _jobs = jobs;
    }

    // Stages the row via Add() only — like ILedgerService.WriteJournalAsync, the caller's own
    // SaveChangesAsync (already required at the end of every handler/job) commits it, so a
    // notification never persists half-committed relative to the change that triggered it.
    public Task NotifyAsync(
        int userId,
        NotificationType type,
        string title,
        string body,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken ct = default)
    {
        _uow.Repository<Notification, int>().Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        _jobs.EnqueueFcmNotification(userId, title, body, referenceType, referenceId);
        return Task.CompletedTask;
    }
}
