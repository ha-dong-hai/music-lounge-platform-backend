using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DomainEventHandlers;

// MLACP-133: "dat ve thanh cong" la mot trong nhung hanh vi DONE WHEN yeu cau ghi lai cho AI hoc,
// nhung truoc day khong noi nao publish tin hieu nay ca — ProcessVnPayCallbackCommandHandler chi
// gui FCM (SendFcmConfirmHandler) va ghi so cai (WriteTicketLedgerHandler), khong co gi cho
// RecomputeUserEventScoresJob/MLNetRecommendationService biet nguoi dung nay vua thuc su mua ve.
internal sealed class LogTicketPurchaseBehaviourHandler : INotificationHandler<TicketPaymentConfirmed>
{
    private readonly IBackgroundJobService _backgroundJobs;

    public LogTicketPurchaseBehaviourHandler(IBackgroundJobService backgroundJobs)
        => _backgroundJobs = backgroundJobs;

    public Task Handle(TicketPaymentConfirmed notification, CancellationToken ct)
    {
        // Walk-in sales publish this event with UserId=0 (no buyer account) — nothing to log
        // (mirrors SendFcmConfirmHandler's same guard for the same reason).
        if (notification.UserId <= 0) return Task.CompletedTask;

        _backgroundJobs.EnqueueLogUserBehaviour(
            notification.UserId, notification.ShowId, BehaviourAction.PurchaseTicket);

        return Task.CompletedTask;
    }
}
