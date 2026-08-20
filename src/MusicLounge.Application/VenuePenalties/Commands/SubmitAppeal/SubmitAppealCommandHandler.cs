using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties.Commands.SubmitAppeal;

internal sealed class SubmitAppealCommandHandler : IRequestHandler<SubmitAppealCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigService _config;
    private readonly ILogger<SubmitAppealCommandHandler> _logger;

    public SubmitAppealCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ISystemConfigService config, ILogger<SubmitAppealCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _config = config;
        _logger = logger;
    }

    public async Task<Unit> Handle(SubmitAppealCommand request, CancellationToken ct)
    {
        var penaltyRepo = _uow.Repository<VenuePenalty, int>();
        var penalty = await penaltyRepo.GetByIdAsync(request.PenaltyId, ct)
            ?? throw new NotFoundException(nameof(VenuePenalty), request.PenaltyId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(penalty.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), penalty.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Chỉ Owner của venue này mới có thể kháng cáo.");

        if (penalty.Status != PenaltyStatus.Active)
            throw new DomainException("Chỉ có thể kháng cáo phạt đang ở trạng thái Active.");

        if (penalty.AppealedAt is not null)
            throw new DomainException("Phạt này đã được kháng cáo trước đó.");

        var now = DateTimeOffset.UtcNow;
        // §6.17 — SLA for Admin to resolve; auto-approved (Overturned) if missed, protecting the
        // Owner from being wrongly penalized by an unattended appeal.
        var slaHours = await _config.GetIntAsync(ConfigKeys.AppealSlaHours, 48, ct);
        penalty.AppealedAt = now;
        penalty.AppealReason = request.AppealReason;
        penalty.AppealDeadline = now.AddHours(slaHours);
        penalty.Status = PenaltyStatus.Appealed;
        penaltyRepo.Update(penalty);
        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Penalty appeal submitted: PenaltyId={PenaltyId} LoungeId={LoungeId} AppealDeadline={AppealDeadline} by OwnerUserId={OwnerUserId} at {At}",
            penalty.Id, penalty.LoungeId, penalty.AppealDeadline, _currentUser.UserId, now);

        var adminIds = await _uow.Repository<User, int>()
            .FindAsync(u => u.Role == UserRole.Admin, ct);
        foreach (var admin in adminIds)
        {
            await _notifications.NotifyAsync(
                admin.Id,
                NotificationType.PenaltyIssued,
                "Có kháng cáo mới cần xử lý",
                $"\"{lounge.Name}\" đã kháng cáo phạt #{penalty.Id} ({penalty.PenaltyType}). " +
                $"Hạn xử lý: {penalty.AppealDeadline:dd/MM/yyyy HH:mm}.",
                referenceType: "venue_penalty",
                referenceId: penalty.Id.ToString(),
                ct: ct);
        }

        return Unit.Value;
    }
}
