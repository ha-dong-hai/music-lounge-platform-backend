using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.Moderations.Commands.ResolveContentReport;

internal sealed class ResolveContentReportCommandHandler : IRequestHandler<ResolveContentReportCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ILivestreamServiceFactory _livestreamFactory;
    private readonly ILivestreamHubService _livestreamHub;
    private readonly ISystemConfigService _config;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<ResolveContentReportCommandHandler> _logger;

    public ResolveContentReportCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        INotificationService notifications,
        ILivestreamRepository livestreamRepo,
        ILivestreamServiceFactory livestreamFactory,
        ILivestreamHubService livestreamHub,
        ISystemConfigService config,
        IAsyncKeyedLock @lock,
        ILogger<ResolveContentReportCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _livestreamRepo = livestreamRepo;
        _livestreamFactory = livestreamFactory;
        _livestreamHub = livestreamHub;
        _config = config;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResolveContentReportCommand request, CancellationToken ct)
    {
        var targetType = Enum.Parse<ReportTargetType>(request.TargetType, ignoreCase: true);
        if (!Enum.TryParse<ContentReportStatus>(request.Action, true, out var resolution)
            || resolution == ContentReportStatus.Open)
            throw new DomainException("Action không hợp lệ. Dùng 'Removed' hoặc 'Dismissed'.");

        await using var _ = await _lock.AcquireAsync($"content-report:{targetType}:{request.TargetId}", ct);

        var reportRepo = _uow.Repository<ContentReport, int>();
        var openReports = await reportRepo.FindAsync(
            r => r.TargetType == targetType && r.TargetId == request.TargetId
                && r.Status == ContentReportStatus.Open, ct);
        if (openReports.Count == 0)
            throw new NotFoundException("Hàng đợi báo cáo cho nội dung này", request.TargetId);

        // NĐ 147/2024: go noi dung phai co hieu luc NGAY truoc khi danh dau report da xu ly xong —
        // neu takedown that bai (vd livestream khong con Live de terminate), cac report van giu
        // Open thay vi bi dong cho 1 hanh dong chua thuc su xay ra.
        if (resolution == ContentReportStatus.Removed)
            await TakeDownAsync(targetType, request.TargetId, request.Note, ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var report in openReports)
        {
            report.Status = resolution;
            report.ResolvedByAdminId = _currentUser.UserId;
            report.ResolutionNote = request.Note;
            report.ResolvedAt = now;
            reportRepo.Update(report);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Content report resolved: TargetType={TargetType} TargetId={TargetId} Resolution={Resolution} " +
            "ReportCount={ReportCount} by AdminUserId={AdminUserId} at {At}",
            targetType, request.TargetId, resolution, openReports.Count, _currentUser.UserId, now);

        return Unit.Value;
    }

    private async Task TakeDownAsync(ReportTargetType targetType, int targetId, string? note, CancellationToken ct)
    {
        switch (targetType)
        {
            case ReportTargetType.Show:
                await TakeDownShowAsync(targetId, ct);
                break;
            case ReportTargetType.Livestream:
                await TakeDownLivestreamAsync(targetId, note, ct);
                break;
            case ReportTargetType.Rating:
                await TakeDownRatingAsync(targetId, note, ct);
                break;
        }
    }

    // Mirrors CancelLoungeShowCommandHandler (huy 100% ve Confirmed + notify tung buyer) — khong
    // goi qua ISender.Send vi handler nay da chay trong transaction cua ResolveContentReportCommand
    // (TransactionBehavior), goi nested se BeginTransactionAsync lan 2 tren cung 1 connection va
    // loi ("connection is already in a transaction"), giong ResolveComplaintCommandHandler.
    private async Task TakeDownShowAsync(int showId, CancellationToken ct)
    {
        var showRepo = _uow.Repository<LoungeShow, int>();
        var show = await showRepo.GetByIdAsync(showId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), showId);

        if (show.Status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended)
            throw new DomainException("Event đã kết thúc hoặc đã bị hủy trước đó.");

        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream?.Status == LivestreamStatus.Live)
            throw new DomainException(
                "Show đang phát trực tiếp — hãy gỡ livestream trước khi có thể gỡ bỏ event.");

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
                Reason = "Nội dung vi phạm bị gỡ bỏ theo báo cáo từ người dùng — hoàn 100% tiền vé",
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

    // Mirrors TerminateLivestreamCommandHandler. Gioi han da biet (cong bo trong PR/Jira): chi go
    // duoc khi livestream dang Live — livestream da Ended thi khong co gi de "dung ngay" nua, Admin
    // chon Dismissed cho truong hop do (vd can go replay sau khi da ket thuc la pham vi khac).
    private async Task TakeDownLivestreamAsync(int livestreamId, string? note, CancellationToken ct)
    {
        var livestream = await _uow.Repository<Livestream, int>().GetByIdAsync(livestreamId, ct)
            ?? throw new NotFoundException(nameof(Livestream), livestreamId);

        if (livestream.Status != LivestreamStatus.Live)
            throw new DomainException(
                "Chỉ có thể gỡ livestream đang phát sóng (status = Live). Nếu đã kết thúc, chọn Dismissed.");

        try
        {
            var provider = _livestreamFactory.GetProvider(livestream.Provider ?? _livestreamFactory.ActiveProviderKey);
            await provider.DeleteStreamAsync(livestream.ProviderRef!, ct);
        }
        catch
        {
            // Best-effort — DB status update la authority.
        }

        var reason = string.IsNullOrWhiteSpace(note) ? "Gỡ theo báo cáo vi phạm từ người dùng" : note;
        var now = DateTimeOffset.UtcNow;

        livestream.Status = LivestreamStatus.Terminated;
        livestream.EndedAt = now;
        livestream.TerminatedById = _currentUser.UserId;
        livestream.TerminatedReason = reason;
        _uow.Repository<Livestream, int>().Update(livestream);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(livestream.LoungeShowId, ct);
        if (show is not null)
        {
            var ratingWindowDays = await _config.GetIntAsync(ConfigKeys.RatingWindowDays, 7, ct);
            show.Status = LoungeShowStatus.Ended;
            show.ActualEnd = now;
            show.RatingOpenUntil = now.AddDays(ratingWindowDays);
            _uow.Repository<LoungeShow, int>().Update(show);
        }

        await _livestreamHub.BroadcastLivestreamTerminatedAsync(livestreamId, reason, ct);
    }

    // Mirrors RemoveRatingCommandHandler.
    private async Task TakeDownRatingAsync(int ratingId, string? note, CancellationToken ct)
    {
        var repo = _uow.Repository<LoungeShowRating, int>();
        var rating = await repo.GetByIdAsync(ratingId, ct)
            ?? throw new NotFoundException(nameof(LoungeShowRating), ratingId);

        if (rating.IsRemoved)
            throw new ConflictException("Đánh giá này đã bị gỡ trước đó.");

        rating.IsRemoved = true;
        rating.RemovedReason = string.IsNullOrWhiteSpace(note) ? "Gỡ theo báo cáo vi phạm từ người dùng" : note;
        repo.Update(rating);
    }
}
