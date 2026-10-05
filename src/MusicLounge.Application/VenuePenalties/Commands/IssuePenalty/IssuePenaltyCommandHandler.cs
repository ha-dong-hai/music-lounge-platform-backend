using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.VenuePenalties.Commands.IssuePenalty;

internal sealed class IssuePenaltyCommandHandler : IRequestHandler<IssuePenaltyCommand, Guid>
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

    public async Task<Guid> Handle(IssuePenaltyCommand request, CancellationToken ct)
    {
        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(request.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), request.LoungeId);

        var penaltyType = Enum.Parse<PenaltyType>(request.PenaltyType, ignoreCase: true);

        // MLACP-676: luật ra án (độ trễ hiệu lực, trạng thái phòng trà, thông báo cho chủ) nằm ở VenuePenaltyIssuer — dùng
        // chung với lệnh Admin xét lý do huỷ buổi.
        var penalty = await VenuePenaltyIssuer.IssueAsync(_uow, _notifications, _config, lounge, penaltyType,
            request.Reason, request.EvidenceRef, request.SuspensionDays, _currentUser.UserId, ct);

        _logger.LogWarning(
            "Venue penalty issued: PenaltyId={PenaltyId} LoungeId={LoungeId} Type={PenaltyType} EffectiveAt={EffectiveAt} by AdminUserId={AdminUserId} at {At}",
            penalty.Id, penalty.LoungeId, penaltyType, penalty.EffectiveAt, _currentUser.UserId, penalty.IssuedAt);

        // NotificationService chi Add() dong thong bao vao change tracker — hop dong ghi ro nguoi goi phai luu — va
        // TransactionBehavior chi Begin/Commit, CommitTransactionAsync cung khong goi SaveChanges. Mot lan luu sau cung ghi ca
        // an phat lan thong bao.
        await _uow.SaveChangesAsync(ct);

        return penalty.Id;
    }
}
