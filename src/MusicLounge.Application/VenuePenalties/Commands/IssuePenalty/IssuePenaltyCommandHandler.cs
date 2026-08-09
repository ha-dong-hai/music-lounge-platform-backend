using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties.Commands.IssuePenalty;

internal sealed class IssuePenaltyCommandHandler : IRequestHandler<IssuePenaltyCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigService _config;
    private readonly ILogger<IssuePenaltyCommandHandler> _logger;

    public IssuePenaltyCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ISystemConfigService config, ILogger<IssuePenaltyCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _config = config;
        _logger = logger;
    }

    public async Task<int> Handle(IssuePenaltyCommand request, CancellationToken ct)
    {
        var loungeRepo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await loungeRepo.GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        var penaltyType = Enum.Parse<PenaltyType>(request.PenaltyType, ignoreCase: true);
        var now = DateTimeOffset.UtcNow;

        // §6.8 — each severity takes effect on its own delay, giving the Owner notice before it
        // bites: warning is immediate, suspension +notice hours, ban +notice days.
        // ApplyDuePenaltiesJob applies the venue-status change and subscription compensation once
        // EffectiveAt arrives.
        var suspensionNoticeHours = await _config.GetIntAsync(ConfigKeys.PenaltySuspensionNoticeHours, 24, ct);
        var banNoticeDays = await _config.GetIntAsync(ConfigKeys.PenaltyBanNoticeDays, 7, ct);
        var effectiveAt = penaltyType switch
        {
            PenaltyType.Warning => now,
            PenaltyType.Suspension => now.AddHours(suspensionNoticeHours),
            PenaltyType.Ban => now.AddDays(banNoticeDays),
            _ => now
        };

        var penalty = new VenuePenalty
        {
            LoungeId = request.LoungeId,
            PenaltyType = penaltyType,
            Reason = request.Reason,
            EvidenceRef = request.EvidenceRef,
            IssuedBy = _currentUser.UserId,
            IssuedAt = now,
            EffectiveAt = effectiveAt,
            SuspensionDays = penaltyType == PenaltyType.Suspension ? request.SuspensionDays : null,
            Status = PenaltyStatus.Active
        };
        _uow.Repository<VenuePenalty, int>().Add(penalty);

        // Warning has no delay and no venue-status/subscription effect (§6.8: "venue vẫn hoạt
        // động, subscription không đổi") — apply it here rather than waiting for the job.
        if (penaltyType == PenaltyType.Warning)
        {
            lounge.Status = LoungeStatus.Warned;
            loungeRepo.Update(lounge);
        }

        await _uow.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(
            lounge.OwnerId,
            NotificationType.PenaltyIssued,
            penaltyType == PenaltyType.Warning ? "Phòng trà bị cảnh cáo" : "Phòng trà bị xử phạt",
            penaltyType switch
            {
                PenaltyType.Warning => $"\"{lounge.Name}\" nhận cảnh cáo: {request.Reason}",
                PenaltyType.Suspension => $"\"{lounge.Name}\" sẽ bị tạm khoá {request.SuspensionDays} ngày " +
                    $"kể từ {effectiveAt:dd/MM/yyyy HH:mm}. Lý do: {request.Reason}. Bạn có thể kháng cáo.",
                PenaltyType.Ban => $"\"{lounge.Name}\" sẽ bị khoá vĩnh viễn kể từ {effectiveAt:dd/MM/yyyy HH:mm}. " +
                    $"Lý do: {request.Reason}. Bạn có thể kháng cáo.",
                _ => request.Reason
            },
            referenceType: "venue_penalty",
            referenceId: penalty.Id.ToString(),
            ct: ct);

        _logger.LogWarning(
            "Venue penalty issued: PenaltyId={PenaltyId} LoungeId={LoungeId} Type={PenaltyType} EffectiveAt={EffectiveAt} by AdminUserId={AdminUserId} at {At}",
            penalty.Id, penalty.LoungeId, penaltyType, effectiveAt, _currentUser.UserId, now);

        return penalty.Id;
    }
}
