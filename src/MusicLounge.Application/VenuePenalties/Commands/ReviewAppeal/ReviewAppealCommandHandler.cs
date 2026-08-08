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

    public ReviewAppealCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILogger<ReviewAppealCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Unit> Handle(ReviewAppealCommand request, CancellationToken ct)
    {
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

        var wasAlreadyApplied = penalty.EffectiveAt <= now
            && penalty.PenaltyType is PenaltyType.Suspension or PenaltyType.Ban;

        if (decision == PenaltyStatus.Overturned)
        {
            // Simplifying assumption: a venue has at most one penalty in effect at a time (no
            // "penalty stack" concept exists in this schema), so reversing always means restoring
            // normal operation rather than falling back to some other still-active penalty.
            lounge.Status = LoungeStatus.Approved;
            _uow.Repository<MusicLoungeEntity, int>().Update(lounge);

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
