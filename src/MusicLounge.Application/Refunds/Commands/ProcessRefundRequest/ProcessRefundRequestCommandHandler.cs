using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Refunds.Commands.ProcessRefundRequest;

internal sealed class ProcessRefundRequestCommandHandler : IRequestHandler<ProcessRefundRequestCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ILedgerService _ledger;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IVnPayService _vnPay;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<ProcessRefundRequestCommandHandler> _logger;

    public ProcessRefundRequestCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILedgerService ledger,
        IPaymentRepository paymentRepo,
        IVnPayService vnPay,
        IAsyncKeyedLock @lock,
        ILogger<ProcessRefundRequestCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ledger = ledger;
        _paymentRepo = paymentRepo;
        _vnPay = vnPay;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<Unit> Handle(ProcessRefundRequestCommand request, CancellationToken ct)
    {
        // 2 lần Admin duyệt gần như đồng thời (double-click, hoặc 2 tab) cho cùng 1
        // RefundRequestId có thể cùng đọc Status==Pending trước khi 1 trong 2 kịp commit — đây là
        // luồng DUY NHẤT trong domain Ticket/Refund thực sự gọi API hoàn tiền thật ra ngoài
        // (VNPay), nên hậu quả của race này nghiêm trọng hơn các luồng khác đã có khóa tương tự
        // (CancelTicket/CheckInTicket/InitiateTicketTransfer).
        await using var _ = await _lock.AcquireAsync($"refund-request:{request.RefundRequestId}", ct);

        var refundRepo = _uow.Repository<RefundRequest, int>();
        var refund = await refundRepo.GetByIdAsync(request.RefundRequestId, ct)
            ?? throw new NotFoundException(nameof(RefundRequest), request.RefundRequestId);

        if (refund.Status != RefundRequestStatus.Pending)
            throw new ConflictException("Yêu cầu hoàn tiền này đã được xử lý trước đó.");

        refund.ProcessedBy = _currentUser.UserId;
        refund.ResolvedAt = DateTimeOffset.UtcNow;

        if (request.Decision == "Rejected")
        {
            refund.Status = RefundRequestStatus.Rejected;
            refundRepo.Update(refund);
            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Refund request rejected: RefundRequestId={RefundRequestId} PaymentId={PaymentId} by AdminUserId={AdminUserId} at {At}",
                refund.Id, refund.PaymentId, _currentUser.UserId, DateTimeOffset.UtcNow);

            return Unit.Value;
        }

        var paymentRepo = _uow.Repository<Payment, int>();
        var payment = await paymentRepo.GetByIdAsync(refund.PaymentId, ct)
            ?? throw new NotFoundException(nameof(Payment), refund.PaymentId);

        var amountApproved = request.ApprovedAmount ?? refund.AmountRequested;
        if (amountApproved > payment.GrossAmount)
            throw new DomainException("Số tiền hoàn không được vượt quá số tiền đã thanh toán.");

        // This refund's own Approved status isn't flushed to the DB yet, so it can't match the
        // query below — compute the cumulative total (other approved refunds + this one) up
        // front instead of relying on a re-query to see it, and reuse it both to reject
        // over-refunding across multiple tickets on the same payment and to decide whether the
        // payment as a whole has now been fully refunded.
        var otherApprovedForPayment = await refundRepo.FindAsync(
            r => r.PaymentId == payment.Id && r.Status == RefundRequestStatus.Approved && r.Id != refund.Id, ct);
        var totalApproved = otherApprovedForPayment.Sum(r => r.AmountApproved ?? 0m) + amountApproved;
        if (totalApproved > payment.GrossAmount)
            throw new DomainException(
                "Tổng số tiền đã hoàn cho giao dịch này sẽ vượt quá số tiền đã thanh toán.");

        var ownerId = await _paymentRepo.GetTicketShowOwnerIdAsync(payment.Id, ct)
            ?? throw new DomainException("Không xác định được chủ phòng trà cho giao dịch này.");

        // MLACP-100: goi VNPay Merchant API that su TRUOC khi dong bo cai — chi ghi so cai/chuyen
        // trang thai neu VNPay xac nhan da hoan tien thanh cong. Chua live-verify duoc chu ky nay
        // voi sandbox that (VNPay mac dinh khoa refund tren tai khoan sandbox, can lien he VNPay
        // de mo — xem comment trong IVnPayService.RefundAsync).
        var vnPayResult = await _vnPay.RefundAsync(new VnPayRefundRequest(
            TxnRef: payment.OrderId,
            Amount: amountApproved,
            OrderInfo: $"Hoan tien yeu cau #{refund.Id}",
            IsFullRefund: amountApproved >= payment.GrossAmount,
            TransactionNo: payment.TransactionId,
            TransactionDate: payment.PaidAt ?? payment.CreatedAt,
            CreatedBy: _currentUser.UserId.ToString(),
            IpAddress: request.ClientIpAddress), ct);

        if (!vnPayResult.IsSuccess)
            throw new ExternalServiceException(
                "VNPay",
                $"Gọi API hoàn tiền VNPay thất bại (mã lỗi {vnPayResult.ResponseCode}): {vnPayResult.Message}. " +
                "Yêu cầu hoàn tiền vẫn ở trạng thái Pending, chưa ghi sổ cái.");

        // Proportional reversal of the original purchase journal (D8 — reverse via offsetting
        // lines, never mutate the original). Owner's share is the remainder rather than its own
        // rounded ratio so debit/credit balance exactly regardless of rounding.
        var ratio = amountApproved / payment.GrossAmount;
        var refundPlatformFee = Math.Round(payment.PlatformFee * ratio, 2);
        var refundTax = Math.Round(payment.TaxWithheld * ratio, 2);
        var refundOwnerNet = amountApproved - refundPlatformFee - refundTax;

        var journalId = Guid.NewGuid().ToString("N");
        await _ledger.WriteJournalAsync(
            journalId,
            LedgerReferenceTypes.Refund,
            refund.Id.ToString(),
            payment.Id,
            new LedgerLine[]
            {
                new(AccountType.Platform, null, refundPlatformFee, IsDebit: true,
                    Description: $"Refund #{refund.Id} — hoàn phí nền tảng"),
                new(AccountType.Tax, null, refundTax, IsDebit: true,
                    Description: $"Refund #{refund.Id} — hoàn thuế"),
                // Owner's share was credited to Platform (held in trust), not to the owner's own
                // User account — see WriteTicketLedgerHandler. Reverse it from the same place.
                // Safe as long as this refund happens before any settlement tranche for this
                // payment has released: CancelTicketCommandHandler only allows cancellation
                // before show.ScheduledStart (CancellationDeadlineHours), while the earliest
                // settlement tranche fires at showEnd+48h — strictly after. If either policy
                // changes such that a refund could land after a tranche is released, the
                // already-released portion needs clawing back from AccountType.User instead.
                new(AccountType.Platform, null, refundOwnerNet, IsDebit: true,
                    Description: $"Refund #{refund.Id} — trừ lại phần giữ hộ chủ phòng trà"),
                new(AccountType.Gateway, null, amountApproved, IsDebit: false,
                    Description: $"Refund #{refund.Id} — hoàn tiền qua cổng thanh toán")
            }, ct);

        refund.Status = RefundRequestStatus.Approved;
        refund.AmountApproved = amountApproved;
        refundRepo.Update(refund);

        // The settlement tranches for this payment were sized against its original gross amount;
        // shrink whichever ones haven't released yet by the same proportion, or
        // SettlementReleaseJob will still pay the owner for a ticket that was refunded.
        var settlementRepo = _uow.Repository<Settlement, int>();
        var pendingSettlements = await settlementRepo.FindAsync(
            s => s.PaymentId == payment.Id && s.Status == SettlementStatus.Scheduled, ct);
        foreach (var settlement in pendingSettlements)
        {
            settlement.NetAmount -= Math.Round(settlement.NetAmount * ratio, 2);
            settlementRepo.Update(settlement);
        }

        // Only flip the payment to Refunded once its total approved refunds cover the full
        // gross amount — a single payment can back multiple tickets, each cancelled/refunded
        // independently, so an early partial refund must not mark the whole payment Refunded.
        if (totalApproved >= payment.GrossAmount)
        {
            payment.Status = PaymentStatus.Refunded;
            paymentRepo.Update(payment);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Refund request approved: RefundRequestId={RefundRequestId} PaymentId={PaymentId} AmountApproved={AmountApproved} by AdminUserId={AdminUserId} at {At}",
            refund.Id, refund.PaymentId, amountApproved, _currentUser.UserId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
}
