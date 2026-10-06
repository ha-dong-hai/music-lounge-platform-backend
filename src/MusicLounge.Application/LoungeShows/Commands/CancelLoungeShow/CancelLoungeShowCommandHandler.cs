using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Domain.ValueObjects;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Application.LoungeShows.Commands.CancelLoungeShow;

// Huy ca show (nang hon ChangeLoungeShowFormat - show khong con dien ra nua) nen phai it nhat
// hao phong bang: hoan 100% CHO MOI ve Confirmed (khong chi rieng Physical nhu doi format,
// vi ca ve Livestream cung mat gia tri khi show bi huy hoan toan) + notify tung ticket holder.
internal sealed class CancelLoungeShowCommandHandler : IRequestHandler<CancelLoungeShowCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly INotificationService _notifications;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly IAsyncKeyedLock _lock;
    private readonly ISystemConfigService _config;

    public CancelLoungeShowCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, INotificationService notifications,
        ILivestreamRepository livestreamRepo, IAsyncKeyedLock @lock, ISystemConfigService config)
    {
        _uow = uow;
        _currentUser = currentUser;
        _notifications = notifications;
        _livestreamRepo = livestreamRepo;
        _lock = @lock;
        _config = config;
    }

    public async Task<Unit> Handle(CancelLoungeShowCommand request, CancellationToken ct)
    {
        // Same key namespace as ChangeLoungeShowFormatCommandHandler — serializes Cancel against a
        // concurrent format-change on the same show too, not just against a double-click of Cancel
        // itself, since both create RefundRequest rows off the same ticket set.
        await using var _ = await _lock.AcquireAsync($"show-status-change:{request.ShowId}", ct);

        var showRepo = _uow.Repository<LoungeShow, Guid>();
        var show = await showRepo.GetByIdAsync(request.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), request.ShowId);

        var lounge = await _uow.Repository<MusicLoungeEntity, Guid>().GetByIdAsync(show.LoungeId, ct)
            ?? throw new NotFoundException(nameof(MusicLoungeEntity), show.LoungeId);

        if (lounge.OwnerId != _currentUser.UserId && _currentUser.Role != "Admin")
            throw new ForbiddenException("Bạn không có quyền huỷ buổi hòa nhạc này.");

        if (show.Status is LoungeShowStatus.Cancelled or LoungeShowStatus.Ended)
            throw new DomainException("Buổi hòa nhạc đã kết thúc hoặc đã bị huỷ trước đó.");

        // Unlike StartLoungeShowCommandHandler (blocks if a livestream row exists at all), a show
        // with a livestream tier that's merely Scheduled/Ended/Terminated is fine to cancel — the
        // one state that must block is Live: cancelling+refunding while it's actively broadcasting
        // to paying viewers would leave the stream running with nothing telling it to stop.
        var livestream = await _livestreamRepo.GetByShowIdAsync(show.Id, ct);
        if (livestream?.Status == LivestreamStatus.Live)
            throw new DomainException(
                "Show đang phát trực tiếp — hãy dừng (terminate) livestream trước khi hủy event.");

        // MLACP-676: chủ phòng trà huỷ buổi ĐÃ MỞ BÁN phải nêu lý do. Huỷ vẫn có hiệu lực NGAY (khán giả được hoàn ngay);
        // lý do vào hàng đợi để Admin xét miễn hay phạt — xem ShowCancellationReview. Admin huỷ thì không tự xét chính mình.
        var laAdmin = _currentUser.Role == "Admin";
        var daMoBan = show.Status is LoungeShowStatus.Published or LoungeShowStatus.Ongoing;
        ShowCancellationReason? lyDo = Enum.TryParse<ShowCancellationReason>(request.Reason, true, out var r) ? r : null;
        var moTa = request.Detail?.Trim();
        if (!laAdmin && daMoBan)
        {
            if (lyDo is null)
                throw new DomainException("Hãy chọn lý do huỷ buổi hòa nhạc — khán giả đã mua vé và nền tảng cần xét lý do này.");
            if (string.IsNullOrWhiteSpace(moTa) || moTa.Length < ShowCancellationReasons.MinDetailLength)
                throw new DomainException(
                    $"Hãy mô tả lý do huỷ cụ thể (ít nhất {ShowCancellationReasons.MinDetailLength} ký tự) để nền tảng xét.");
        }

        // Thiệt hại cho khán giả, chụp TRƯỚC khi huỷ (sau đó vé đã thành Cancelled): vé bán qua nền tảng và tiền phải hoàn.
        var veDaBan = await _uow.Repository<Ticket, Guid>().FindAsync(
            t => t.ShowId == show.Id && t.Status == TicketStatus.Confirmed && t.PaymentId != null, ct);
        var priceIds = veDaBan.Select(t => t.PriceId).Distinct().ToList();
        var giaTheoId = priceIds.Count == 0 ? new Dictionary<Guid, decimal>()
            : (await _uow.Repository<TicketPrice, Guid>().FindAsync(p => priceIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.Price);

        // MLACP-373: phan huy — hoan 100% moi ve, bao nguoi giu ve — nam o ShowCancellation, dung chung voi job ap an
        // phat khi phong tra bi khoa / tam khoa. MLACP-632: chu phong tra huy buoi thi don do uong GIU NGUYEN, khach
        // duoc bao va tu quyet (chu du an chot 04/10/2026) — thay cho MLACP-380 (huy luon don F&B gan voi show).
        // MLACP-676: khán giả được nói lý do ở dạng trung tính (ShowCancellationReasons.ForBuyers).
        await ShowCancellation.CancelAsync(_uow, _notifications, _lock, show,
            why: lyDo is { } loai ? ShowCancellationReasons.ForBuyers(loai) : null, cancelFnbOrders: false, ct);

        if (!laAdmin && daMoBan && lyDo is { } loaiLyDo)
            await MoHangXetAsync(show, lounge, loaiLyDo, moTa!, request.EvidenceUrl,
                veDaBan.Count, veDaBan.Sum(t => giaTheoId.GetValueOrDefault(t.PriceId)), ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task MoHangXetAsync(
        LoungeShow show, MusicLoungeEntity lounge, ShowCancellationReason lyDo, string moTa, string? evidenceUrl,
        int soVe, decimal tienHoan, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var slaHours = await _config.GetIntAsync(ConfigKeys.ModerationSlaHours, 24, ct);
        var review = new ShowCancellationReview
        {
            ShowId = show.Id,
            LoungeId = lounge.Id,
            CancelledBy = _currentUser.UserId,
            Reason = lyDo,
            Detail = moTa,
            EvidenceUrl = string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl.Trim(),
            TicketsRefunded = soVe,
            AmountRefunded = tienHoan,
            CreatedAt = now,
            SlaDeadline = now.AddHours(slaHours)
        };
        _uow.Repository<ShowCancellationReview, Guid>().Add(review);

        var nhan = ShowCancellationReasons.Label(lyDo);
        var admins = await _uow.Repository<User, Guid>().FindAsync(u => u.Role == UserRole.Admin, ct);
        foreach (var admin in admins)
            await _notifications.NotifyAsync(
                admin.Id, NotificationType.ShowCancellationReview,
                new SongNgu("Phòng trà huỷ buổi hòa nhạc — cần xét lý do", "A venue cancelled a concert — reason needs review"),
                new SongNgu(
                    $"\"{lounge.Name}\" huỷ \"{show.Name}\" ({soVe} vé, hoàn {VietnamMoney.Format(tienHoan)}). Lý do: {nhan.Vi}. " +
                    "Hãy xét miễn hay phạt.",
                    $"\"{lounge.Name}\" cancelled \"{show.Name}\" ({soVe} tickets, {tienHoan:N0} VND refunded). Reason: {nhan.En}. " +
                    "Please decide whether to excuse or penalise."),
                referenceType: "show_cancellation_review", referenceId: review.Id.ToString(), ct: ct);
    }
}
