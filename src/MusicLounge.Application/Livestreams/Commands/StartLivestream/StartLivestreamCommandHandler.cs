using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Livestreams.Commands.StartLivestream;

internal sealed class StartLivestreamCommandHandler : IRequestHandler<StartLivestreamCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IEventModerationRepository _moderationRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly IFollowRepository _followRepo;
    private readonly INotificationService _notifications;

    public StartLivestreamCommandHandler(
        IUnitOfWork uow,
        IEventModerationRepository moderationRepo,
        ICurrentUserService currentUser,
        ILivestreamRepository livestreamRepo,
        IFollowRepository followRepo,
        INotificationService notifications)
    {
        _uow = uow;
        _moderationRepo = moderationRepo;
        _currentUser = currentUser;
        _livestreamRepo = livestreamRepo;
        _followRepo = followRepo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(StartLivestreamCommand request, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(request.LivestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), request.LivestreamId);

        if (livestream.Status != LivestreamStatus.Scheduled)
            throw new DomainException($"Không thể bắt đầu livestream ở trạng thái '{livestream.Status}'.");

        // W08: require Admin approval before going live
        var moderation = await _moderationRepo.GetByTargetAsync(
            ModerationTargetType.Livestream, request.LivestreamId, ct);

        if (moderation is null || moderation.AdminDecision != ModerationDecision.Approved)
            throw new DomainException("Livestream chưa được Admin duyệt. Không thể phát sóng.");

        // D6: Staff chỉ được start livestream của venue được phân công (lounge_id từ JWT)
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), livestream.LoungeShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);
        if (!VenueOperatorAccess.CanOperate(_currentUser, show.LoungeId, lounge.OwnerId))
            throw new ForbiddenException("Bạn không có quyền phát livestream của venue này.");

        // D19: phai tra tac quyen VCPMC truoc khi show dien ra
        if (string.IsNullOrWhiteSpace(show.VcpmcRoyaltyReference))
            throw new DomainException(
                "Cần khai báo đã thanh toán tác quyền VCPMC trước khi bắt đầu show.");

        livestream.Status = LivestreamStatus.Live;
        livestream.StartedAt = DateTimeOffset.UtcNow;
        _uow.Repository<Livestream, int>().Update(livestream);

        show.Status = LoungeShowStatus.Ongoing;
        show.ActualStart = DateTimeOffset.UtcNow;
        _uow.Repository<LoungeShow, int>().Update(show);

        await _uow.SaveChangesAsync(ct);

        // Backfill access tokens for confirmed Livestream-tier tickets purchased before stream was started
        var ticketIds = await _livestreamRepo
            .GetConfirmedLivestreamTicketIdsWithoutDetailAsync(livestream.LoungeShowId, ct);
        foreach (var ticketId in ticketIds)
            _livestreamRepo.AddTicketDetail(new LivestreamTicketDetail
            {
                TicketId = ticketId,
                LivestreamId = livestream.Id,
                AccessToken = Guid.NewGuid().ToString("N")
            });
        if (ticketIds.Count > 0)
            await _uow.SaveChangesAsync(ct);

        await NotifyLiveAsync(show, ct);

        return Unit.Value;
    }

    private async Task NotifyLiveAsync(LoungeShow show, CancellationToken ct)
    {
        var ticketHolderIds = (await _uow.Repository<Ticket, Guid>().FindAsync(
                t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed && t.BuyerId != null, ct))
            .Select(t => t.BuyerId!.Value);
        var followerIds = await _followRepo.GetFollowerUserIdsAsync(show.LoungeId, ct);

        var recipientIds = ticketHolderIds.Union(followerIds).Distinct();
        foreach (var userId in recipientIds)
        {
            await _notifications.NotifyAsync(
                userId,
                NotificationType.EventLive,
                "Đang phát trực tiếp!",
                $"\"{show.Name}\" đang livestream ngay bây giờ.",
                referenceType: "show",
                referenceId: show.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
