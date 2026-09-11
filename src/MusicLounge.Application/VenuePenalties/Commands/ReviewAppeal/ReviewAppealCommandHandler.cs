using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties.Commands.ReviewAppeal;

internal sealed class ReviewAppealCommandHandler : IRequestHandler<ReviewAppealCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILogger<ReviewAppealCommandHandler> _logger;
    private readonly IAsyncKeyedLock _lock;

    public ReviewAppealCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILogger<ReviewAppealCommandHandler> logger, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _logger = logger;
        _lock = @lock;
    }

    public async Task<Unit> Handle(ReviewAppealCommand request, CancellationToken ct)
    {
        // Same key AutoApproveOverdueAppealsJob locks on — an Admin manually deciding right at the
        // 48h SLA boundary can otherwise race the auto-approve job: both read Status==Appealed
        // before either commits, both write a (possibly contradictory) decision.
        await using var _ = await _lock.AcquireAsync($"appeal-review:{request.PenaltyId}", ct);

        var penaltyRepo = _uow.Repository<VenuePenalty, int>();
        var penalty = await penaltyRepo.GetByIdAsync(request.PenaltyId, ct)
            ?? throw new NotFoundException(nameof(VenuePenalty), request.PenaltyId);

        if (penalty.Status != PenaltyStatus.Appealed)
            throw new DomainException("Chỉ có thể xử lý kháng cáo đang ở trạng thái Appealed.");

        var decision = Enum.Parse<PenaltyStatus>(request.Decision, ignoreCase: true);
        var now = DateTimeOffset.UtcNow;

        penalty.AppealResult = decision.ToString();
        penalty.Status = decision;
        penalty.ReviewedBy = _currentUser.UserId;
        penalty.ReviewedAt = now;
        penalty.CompensationNote = request.ReviewNote;
        penaltyRepo.Update(penalty);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(penalty.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), penalty.LoungeId);

        // Exact now (was inferred from EffectiveAt <= now, which could be wrong in the window
        // after EffectiveAt passes but before ApplyDuePenaltiesJob has actually ticked).
        var wasAlreadyApplied = penalty.AppliedAt is not null;
        OwnerSubscription? restoredPlan = null;

        if (decision == PenaltyStatus.Overturned)
        {
            // A venue CAN have more than one penalty in effect at once — its status afterwards comes from
            // whatever is STILL in force. MLACP-367: the same rule every other place uses (PenaltyLifecycle).
            // This used to count only Suspension/Ban, so lifting one warning while another stood reset the
            // venue to Approved; and it reset a status no penalty had set.
            var remaining = await _uow.Repository<VenuePenalty, int>().FindAsync(
                p => p.LoungeId == penalty.LoungeId
                    && p.Id != penalty.Id
                    && PenaltyLifecycle.InForce.Contains(p.Status),
                ct);

            if (PenaltyLifecycle.StatusAfterReleasing(lounge.Status, penalty.PenaltyType, remaining) is { } releasedStatus)
            {
                lounge.Status = releasedStatus;
                _uow.Repository<MusicLoungeEntity, int>().Update(lounge);
            }

            // MLACP-369: khoa vinh vien da ap roi bi huy thi tra lai goi cho chu — dung phan thoi gian con lai
            // luc bi khoa. Truoc day chi nhan Admin "xu ly thu cong" (va but toan "hoan tien" luc khoa khong he
            // co tien that). Tam khoa da ap thi khong co gi phai hoan tac: so ngay da bu vao goi la de bu cho
            // thoi gian bi khoa — bi khoa oan thi cang dang duoc giu.
            if (wasAlreadyApplied && penalty.PenaltyType == PenaltyType.Ban)
            {
                var ownerSubscriptions = await _uow.Repository<OwnerSubscription, int>().FindAsync(
                    s => s.OwnerId == lounge.OwnerId, ct);
                restoredPlan = PenaltySubscriptions.RestoreAfterBanLifted(ownerSubscriptions, penalty, now);
                if (restoredPlan is not null)
                    _uow.Repository<OwnerSubscription, int>().Update(restoredPlan);
            }
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Penalty appeal reviewed: PenaltyId={PenaltyId} LoungeId={LoungeId} Decision={Decision} by AdminUserId={AdminUserId} at {At}",
            penalty.Id, penalty.LoungeId, decision, _currentUser.UserId, now);

        await _notifications.NotifyAsync(
            lounge.OwnerId,
            NotificationType.AppealResolved,
            decision == PenaltyStatus.Overturned ? "Kháng cáo được chấp thuận" : "Kháng cáo bị từ chối",
            decision == PenaltyStatus.Overturned
                ? $"Kháng cáo của bạn cho phạt #{penalty.Id} đã được chấp thuận. {PenaltyLifecycle.DescribeForOwner(lounge.Status)}".TrimEnd() +
                  (restoredPlan is null ? "" : $" Gói dịch vụ đã được kích hoạt lại, hết hạn {VietnamTime.Format(restoredPlan.ExpiresAt, "dd/MM/yyyy")}.")
                : $"Kháng cáo của bạn cho phạt #{penalty.Id} bị từ chối. {request.ReviewNote ?? ""}".Trim(),
            referenceType: "venue_penalty",
            referenceId: penalty.Id.ToString(),
            ct: ct);


        // Luu SAU khi gui thong bao. NotificationService chi Add() dong thong bao vao change
        // tracker — hop dong ghi ro nguoi goi phai luu — va TransactionBehavior chi Begin/Commit,
        // CommitTransactionAsync cung khong goi SaveChanges. Luu truoc roi moi Notify nghia la
        // dong thong bao duoc them vao bo nho roi bien mat, khong bao loi gi ca.
        await _uow.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
