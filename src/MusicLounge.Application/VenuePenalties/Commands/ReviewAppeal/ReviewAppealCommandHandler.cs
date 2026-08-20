using MediatR;
using Microsoft.Extensions.Logging;
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

        if (decision == PenaltyStatus.Overturned)
        {
            // A venue CAN have more than one Suspension/Ban in effect at once (e.g. a second
            // violation while the first is still unresolved) — only reset to Approved if no OTHER
            // currently-in-effect penalty still justifies keeping this venue locked/suspended.
            // Previously this reset unconditionally, which could re-open a venue that another,
            // still-active penalty should have kept locked.
            var otherActivePenalties = await _uow.Repository<VenuePenalty, int>().FindAsync(
                p => p.LoungeId == penalty.LoungeId
                    && p.Id != penalty.Id
                    && p.Status == PenaltyStatus.Active
                    && p.AppliedAt != null
                    && (p.PenaltyType == PenaltyType.Suspension || p.PenaltyType == PenaltyType.Ban),
                ct);

            if (otherActivePenalties.Count == 0)
            {
                lounge.Status = LoungeStatus.Approved;
                _uow.Repository<MusicLoungeEntity, int>().Update(lounge);
            }

            if (wasAlreadyApplied)
            {
                // The suspension-day extension or ban pro-rata refund (ApplyDuePenaltiesJob) has
                // already gone through by the time this appeal was resolved — reversing a
                // subscription extension or a ledger-recorded refund automatically risks getting
                // the money side wrong. Surface it for a human instead of guessing.
                await NotifyAdminsManualReversalNeededAsync(penalty, lounge, ct);
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
                ? $"Kháng cáo của bạn cho phạt #{penalty.Id} đã được chấp thuận. Venue trở lại hoạt động bình thường."
                : $"Kháng cáo của bạn cho phạt #{penalty.Id} bị từ chối. {request.ReviewNote ?? ""}".Trim(),
            referenceType: "venue_penalty",
            referenceId: penalty.Id.ToString(),
            ct: ct);

        return Unit.Value;
    }

    private async Task NotifyAdminsManualReversalNeededAsync(
        VenuePenalty penalty, MusicLoungeEntity lounge, CancellationToken ct)
    {
        var adminIds = await _uow.Repository<User, int>().FindAsync(u => u.Role == UserRole.Admin, ct);
        foreach (var admin in adminIds)
        {
            await _notifications.NotifyAsync(
                admin.Id,
                NotificationType.AppealResolved,
                "Cần xử lý thủ công: hoàn tác bù trừ subscription",
                $"Phạt #{penalty.Id} ({penalty.PenaltyType}) trên \"{lounge.Name}\" đã được overturn " +
                "sau khi bù trừ subscription (gia hạn/hoàn tiền) đã áp dụng. Vui lòng kiểm tra và " +
                "điều chỉnh owner_subscriptions/ledger thủ công cho đúng.",
                referenceType: "venue_penalty",
                referenceId: penalty.Id.ToString(),
                ct: ct);
        }
    }
}
