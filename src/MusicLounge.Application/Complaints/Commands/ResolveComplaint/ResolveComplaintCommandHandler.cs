using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Complaints.Commands.ResolveComplaint;

internal sealed class ResolveComplaintCommandHandler : IRequestHandler<ResolveComplaintCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<ResolveComplaintCommandHandler> _logger;

    public ResolveComplaintCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILivestreamRepository livestreamRepo, IAsyncKeyedLock @lock,
        ILogger<ResolveComplaintCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _livestreamRepo = livestreamRepo;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResolveComplaintCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Complaint, int>();
        var complaint = await repo.GetByIdAsync(request.ComplaintId, ct)
            ?? throw new NotFoundException(nameof(Complaint), request.ComplaintId);

        if (complaint.Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
            throw new ConflictException("Khiếu nại này đã được xử lý xong.");

        var newStatus = Enum.Parse<ComplaintStatus>(request.Status, ignoreCase: true);
        var resolvedAction = request.ResolvedAction is null
            ? (ComplaintResolvedAction?)null
            : Enum.Parse<ComplaintResolvedAction>(request.ResolvedAction, ignoreCase: true);

        // NĐ 147/2024: take down the violating content BEFORE marking the complaint itself
        // resolved — if the show can't actually be cancelled (e.g. its livestream is Live and must
        // be terminated first), the complaint stays open rather than getting marked Resolved for an
        // action that never actually happened.
        if (newStatus == ComplaintStatus.Resolved && resolvedAction == ComplaintResolvedAction.TakeDownContent)
        {
            if (complaint.TargetType != "show")
                throw new DomainException(
                    "TakeDownContent chỉ áp dụng cho khiếu nại về show (TargetType = \"show\").");

            await TakeDownShowAsync(complaint.TargetId, ct);
        }

        // MLACP-198: "phạt venue" phải thực sự tạo VenuePenalty — truoc day IssueWarning chi la 1
        // nhan luu tren Complaint, khong co hau qua that nao (venue van hoat dong binh thuong).
        if (newStatus == ComplaintStatus.Resolved && resolvedAction == ComplaintResolvedAction.IssueWarning)
            await IssueVenuePenaltyAsync(complaint, ct);

        complaint.Status = newStatus;
        complaint.AdminId = _currentUser.UserId;

        if (newStatus is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
        {
            complaint.Resolution = request.Resolution;
            complaint.ResolvedAction = resolvedAction;
            complaint.ResolvedAt = DateTimeOffset.UtcNow;
        }

        repo.Update(complaint);

        if (complaint.ComplainantUserId is int complainantId)
        {
            await _notifications.NotifyAsync(
                complainantId,
                NotificationType.ComplaintUpdate,
                "Cập nhật khiếu nại",
                newStatus == ComplaintStatus.Resolved
                    ? "Khiếu nại của bạn đã được xử lý."
                    : newStatus == ComplaintStatus.Rejected
                        ? "Khiếu nại của bạn đã bị từ chối."
                        : "Khiếu nại của bạn đang được xem xét.",
                referenceType: "complaint",
                referenceId: complaint.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Complaint resolved: ComplaintId={ComplaintId} NewStatus={NewStatus} ResolvedAction={ResolvedAction} by AdminUserId={AdminUserId} at {At}",
            complaint.Id, newStatus, complaint.ResolvedAction, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }

    // Mirrors CancelLoungeShowCommandHandler's cancel + 100%-refund-every-confirmed-ticket logic
    // (same lock key, same guards, same buyer protection) rather than composing it via
    // ISender.Send(new CancelLoungeShowCommand(...)) — this handler already runs inside
    // TransactionBehavior's transaction for ResolveComplaintCommand, and a nested Send would try to
    // BeginTransactionAsync a second time on the same connection ("connection is already in a
    // transaction"). Keep this in sync with CancelLoungeShowCommandHandler if that logic changes.
    private async Task TakeDownShowAsync(int showId, CancellationToken ct)
    {
        await using var _ = await _lock.AcquireAsync($"show-status-change:{showId}", ct);

        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(showId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), showId);

        if (show.Status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended)
            throw new DomainException("Event đã kết thúc hoặc đã bị hủy trước đó.");

        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream?.Status == LivestreamStatus.Live)
            throw new DomainException(
                "Show đang phát trực tiếp — hãy dừng (terminate) livestream trước khi có thể gỡ bỏ nội dung.");

        show.Status = LoungeShowStatus.Cancelled;
        showRepo.Update(show);

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        var confirmedTickets = await ticketRepo.FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed, ct);

        if (confirmedTickets.Count == 0) return;

        var priceIds = confirmedTickets.Select(t => t.PriceId).Distinct().ToList();
        var prices = await _uow.Repository<TicketPrice, int>().FindAsync(p => priceIds.Contains(p.Id), ct);
        var priceById = prices.ToDictionary(p => p.Id, p => p.Price);

        var refundRepo = _uow.Repository<RefundRequest, int>();
        var now = DateTimeOffset.UtcNow;

        foreach (var ticket in confirmedTickets)
        {
            ticket.Status = TicketStatus.Cancelled;
            ticketRepo.Update(ticket);

            if (ticket.PaymentId is null) continue;

            refundRepo.Add(new RefundRequest
            {
                PaymentId = ticket.PaymentId.Value,
                RequestedBy = ticket.BuyerId,
                Reason = "Nội dung vi phạm bị gỡ bỏ theo khiếu nại — hoàn 100% tiền vé",
                AmountRequested = priceById.GetValueOrDefault(ticket.PriceId),
                RefundPercentage = 100m,
                Status = RefundRequestStatus.Pending,
                CreatedAt = now
            });

            if (ticket.BuyerId is int buyerId)
                await _notifications.NotifyAsync(
                    buyerId,
                    NotificationType.EventCancelled,
                    "Event đã bị gỡ bỏ",
                    $"\"{show.Name}\" đã bị gỡ bỏ do vi phạm nội dung. Vé của bạn đã được hủy và tự động " +
                    "tạo yêu cầu hoàn 100% tiền vé.",
                    referenceType: "show",
                    referenceId: show.Id.ToString(),
                    ct: ct);
        }
    }

    // Mirrors IssuePenaltyCommandHandler's Warning path (immediate effect, no notice delay) rather
    // than composing it via ISender.Send — same nested-transaction restriction as TakeDownShowAsync.
    private async Task IssueVenuePenaltyAsync(Complaint complaint, CancellationToken ct)
    {
        var loungeId = await ResolveLoungeIdAsync(complaint, ct)
            ?? throw new DomainException(
                "Không thể xác định venue để xử phạt từ khiếu nại này (TargetType không liên kết được tới 1 venue cụ thể).");

        var loungeRepo = _uow.Repository<MusicLoungeEntity, int>();
        var lounge = await loungeRepo.GetByIdAsync(loungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), loungeId);

        var now = DateTimeOffset.UtcNow;
        var penalty = new VenuePenalty
        {
            LoungeId = loungeId,
            PenaltyType = PenaltyType.Warning,
            Reason = $"Xử lý theo khiếu nại #{complaint.Id}: {complaint.Description}",
            IssuedBy = _currentUser.UserId,
            IssuedAt = now,
            EffectiveAt = now,
            Status = PenaltyStatus.Active
        };
        _uow.Repository<VenuePenalty, int>().Add(penalty);

        lounge.Status = LoungeStatus.Warned;
        loungeRepo.Update(lounge);

        await _notifications.NotifyAsync(
            lounge.OwnerId,
            NotificationType.PenaltyIssued,
            "Phòng trà bị cảnh cáo",
            $"\"{lounge.Name}\" nhận cảnh cáo theo khiếu nại #{complaint.Id}: {complaint.Description}",
            referenceType: "venue_penalty",
            referenceId: penalty.Id.ToString(),
            ct: ct);
    }

    private async Task<int?> ResolveLoungeIdAsync(Complaint complaint, CancellationToken ct) =>
        complaint.TargetType switch
        {
            "venue" => complaint.TargetId,
            "show" => (await _uow.Repository<LoungeShow, int>().GetByIdAsync(complaint.TargetId, ct))?.LoungeId,
            "livestream" => (await _livestreamRepo.GetByIdAsync(complaint.TargetId, ct)) is { } livestream
                ? (await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct))?.LoungeId
                : null,
            "donation" => await ResolveLoungeIdFromDonationAsync(complaint.TargetId, ct),
            _ => null
        };

    private async Task<int?> ResolveLoungeIdFromDonationAsync(int donationId, CancellationToken ct)
    {
        var donation = await _uow.Repository<Donation, int>().GetByIdAsync(donationId, ct);
        if (donation is null) return null;
        var performance = await _uow.Repository<Performance, int>().GetByIdAsync(donation.PerformanceId, ct);
        if (performance is null) return null;
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(performance.LoungeShowId, ct);
        return show?.LoungeId;
    }
}
