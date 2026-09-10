using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Settlements.Commands.ReviewSettlement;

/// <summary>
/// MLACP-335. Trước task này, một khoản bị chốt D16 giữ lại thành <c>PendingReview</c> là đi vào ngõ
/// cụt: không endpoint, không command, không query nào cho Settlement tồn tại, và
/// <c>SettlementReleaseJob</c> chỉ lấy <c>Scheduled</c> nên không bao giờ ngó lại. Tiền của phòng
/// trà nằm đó vĩnh viễn, mà <c>GetMyEarnings</c> vẫn đếm nó vào mục sắp nhận được.
/// </summary>
internal sealed class ReviewSettlementCommandHandler : IRequestHandler<ReviewSettlementCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ILedgerService _ledger;
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ReviewSettlementCommandHandler> _logger;

    public ReviewSettlementCommandHandler(
        IUnitOfWork uow,
        ILedgerService ledger,
        INotificationService notifications,
        ICurrentUserService currentUser,
        ILogger<ReviewSettlementCommandHandler> logger)
    {
        _uow = uow;
        _ledger = ledger;
        _notifications = notifications;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Unit> Handle(ReviewSettlementCommand request, CancellationToken ct)
    {
        var repo = _uow.Repository<Settlement, int>();
        var settlement = await repo.GetByIdAsync(request.SettlementId, ct)
            ?? throw new NotFoundException(nameof(Settlement), request.SettlementId);

        // Chỉ quyết được đúng một lần. Sổ cái chỉ ghi thêm chứ không sửa được, nên giải ngân hai
        // lần cho cùng một tranche phải gỡ bằng bút toán đảo thủ công.
        if (settlement.Status != SettlementStatus.PendingReview)
            throw new ConflictException(
                $"Khoản quyết toán này không còn chờ duyệt (đang ở trạng thái {settlement.Status}).");

        if (request.Decision == "Withhold")
        {
            settlement.Status = SettlementStatus.Cancelled;
            repo.Update(settlement);

            _logger.LogWarning(
                "Admin giu lai khoan quyet toan — SettlementId={SettlementId} OwnerId={OwnerId} " +
                "SoTien={Amount} AdminId={AdminId} LyDo={Note} at {At}",
                settlement.Id, settlement.OwnerId, settlement.NetAmount,
                _currentUser.UserId, request.Note, DateTimeOffset.UtcNow);

            // Nói đúng chuyện đã xảy ra và không hứa gì thêm: việc người mua vé có được hoàn tiền
            // hay không đi qua luồng hoàn tiền riêng, không phải hệ quả tự động của quyết định này.
            await _notifications.NotifyAsync(
                settlement.OwnerId,
                NotificationType.SettlementWithheld,
                "Khoản quyết toán bị giữ lại",
                $"Khoản {settlement.NetAmount:N0}đ ({settlement.ReleaseType}) không được chi trả. " +
                $"Lý do: {request.Note}",
                referenceType: "settlement",
                referenceId: settlement.Id.ToString(),
                ct: ct);

            await _uow.SaveChangesAsync(ct);
            return Unit.Value;
        }

        // ── Release ──────────────────────────────────────────────────────────────
        // Hai chốt dưới đây lặp lại đúng hai chốt SettlementReleaseJob áp trước khi giải ngân. Cố ý
        // lặp chứ không bỏ: đây là một đường giải ngân thứ hai, và một đường giải ngân không có
        // chốt thì chính nó là lỗ hổng.

        if (settlement.BankAccountId is null)
            throw new DomainException(
                "Phòng trà chưa đăng ký tài khoản nhận tiền — ghi bút toán chi trả bây giờ sẽ ghi có " +
                "cho một khoản không lệnh chuyển khoản nào đi theo được.");

        var hasPendingRefund = await _uow.Repository<RefundRequest, int>().AnyAsync(
            r => r.PaymentId == settlement.PaymentId && r.Status == RefundRequestStatus.Pending, ct);
        if (hasPendingRefund)
            throw new DomainException(
                "Giao dịch này còn yêu cầu hoàn tiền chưa xử lý. Xử lý hoàn tiền trước, nếu không " +
                "phòng trà nhận đủ tiền cho phần sắp phải trả lại cho khách.");

        var journalId = Guid.NewGuid().ToString("N");
        await _ledger.WriteJournalAsync(
            journalId,
            LedgerReferenceTypes.Settlement,
            settlement.Id.ToString(),
            settlement.PaymentId,
            SettlementPayout.JournalLines(settlement),
            ct);

        settlement.Status = SettlementStatus.Released;
        settlement.ReleasedAt = DateTimeOffset.UtcNow;
        settlement.LedgerJournalId = journalId;
        repo.Update(settlement);

        _logger.LogInformation(
            "Admin duyet chi tra khoan quyet toan — SettlementId={SettlementId} OwnerId={OwnerId} " +
            "SoTien={Amount} AdminId={AdminId} LyDo={Note} at {At}",
            settlement.Id, settlement.OwnerId, settlement.NetAmount,
            _currentUser.UserId, request.Note, DateTimeOffset.UtcNow);

        await _notifications.NotifyAsync(
            settlement.OwnerId,
            NotificationType.SettlementReleased,
            "Khoản quyết toán đã được giải ngân",
            $"Khoản thanh toán {settlement.NetAmount:N0}đ ({settlement.ReleaseType}) đã được giải ngân.",
            referenceType: "settlement",
            referenceId: settlement.Id.ToString(),
            ct: ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
