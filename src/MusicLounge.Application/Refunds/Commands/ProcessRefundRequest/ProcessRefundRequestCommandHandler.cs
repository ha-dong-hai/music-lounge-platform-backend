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
    private readonly ISystemConfigService _config;
    private readonly ILogger<ProcessRefundRequestCommandHandler> _logger;

    public ProcessRefundRequestCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILedgerService ledger,
        IPaymentRepository paymentRepo,
        IVnPayService vnPay,
        IAsyncKeyedLock @lock,
        ISystemConfigService config,
        ILogger<ProcessRefundRequestCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ledger = ledger;
        _paymentRepo = paymentRepo;
        _vnPay = vnPay;
        _lock = @lock;
        _config = config;
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

        // Checked here, before the owner lookup and the over-refund arithmetic below: this is a
        // precondition of the PAYMENT alone, and it should answer the same way whether or not the
        // rest of the chain happens to resolve. VNPay's merchant terms cap a refund at 3 months from the transaction. Past that the
        // gateway refuses the reversal outright, so calling it would fail with a bare response code
        // and leave the Admin guessing. Say plainly what happened and what has to be done instead —
        // the buyer is still owed the money, it just cannot travel back down the same rails.
        var refundWindowDays = await _config.GetIntAsync(ConfigKeys.VnPayRefundWindowDays, 90, ct);
        var transactionAt = payment.PaidAt ?? payment.CreatedAt;
        if (transactionAt.AddDays(refundWindowDays) < DateTimeOffset.UtcNow)
            throw new DomainException(
                $"Giao dịch này đã quá {refundWindowDays} ngày kể từ lúc thanh toán " +
                $"({transactionAt:dd/MM/yyyy}), vượt quá thời hạn VNPay còn nhận lệnh hoàn tiền. " +
                "Không thể hoàn tự động — cần chuyển khoản thủ công cho người mua rồi ghi nhận lại, " +
                "và yêu cầu này vẫn giữ nguyên trạng thái chờ xử lý.");

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
        // Withheld personal income tax is given back on the same proportional basis as VAT. Both
        // are reversed from the amounts SNAPSHOTTED ON THE PAYMENT, never recomputed from today's
        // rates or today's classification of the seller — the money to give back is the money that
        // was actually taken. NĐ 117/2025 provides for offsetting withheld tax against cancelled
        // and returned transactions, so a refund that kept the tax would be wrong twice over: the
        // buyer is short, and the platform holds a withholding for revenue that no longer exists.
        var refundPersonalIncomeTax = Math.Round(payment.PersonalIncomeTaxWithheld * ratio, 2);
        var refundOwnerNet = amountApproved - refundPlatformFee - refundTax - refundPersonalIncomeTax;

        // The owner's share was credited to Platform (held in trust) at purchase, then moved to the
        // owner's own User account by each SettlementReleaseJob tranche. Which account still holds
        // it therefore depends on how much has already been released, and the reversal has to debit
        // wherever the money actually IS — debiting Platform for a tranche already paid out takes it
        // from an account that no longer holds it and leaves the owner keeping money for a refunded
        // ticket, with only GetLedgerIntegrity noticing afterwards.
        //
        // This was previously assumed unreachable, on the grounds that a refund could only be raised
        // before the show started while the first tranche fires at showEnd+48h. That assumption does
        // not hold: CancellationDeadlineHours is optional (MLACP-257), and CancelTicket's only hard
        // stop is show.Status == Ended — but nothing ever ends a show automatically. An offline show
        // whose Owner never pressed "End" stays Published forever, so both tranches release (Final30
        // included: its completion check returns true precisely because ActualStart/ActualEnd are
        // null) and a ticket stays cancellable weeks afterwards.
        var releasedToOwner = (await _uow.Repository<Settlement, int>().FindAsync(
                s => s.PaymentId == payment.Id && s.Status == SettlementStatus.Released, ct))
            .Sum(s => s.NetAmount);
        var stillHeldByPlatform = Math.Max(0m, payment.NetAmount - releasedToOwner);

        var reverseFromPlatform = Math.Min(refundOwnerNet, stillHeldByPlatform);
        var reverseFromOwner = refundOwnerNet - reverseFromPlatform;

        var ownerShareLines = new List<LedgerLine>();
        if (reverseFromPlatform > 0m)
            ownerShareLines.Add(new LedgerLine(AccountType.Platform, null, reverseFromPlatform, IsDebit: true,
                Description: $"Refund #{refund.Id} — trừ lại phần giữ hộ chủ phòng trà"));
        if (reverseFromOwner > 0m)
        {
            ownerShareLines.Add(new LedgerLine(AccountType.User, ownerId, reverseFromOwner, IsDebit: true,
                Description: $"Refund #{refund.Id} — thu hồi phần đã giải ngân cho chủ phòng trà"));
            _logger.LogWarning(
                "Refund claws back already-released settlement money: RefundRequestId={RefundRequestId} " +
                "PaymentId={PaymentId} OwnerId={OwnerId} FromOwner={FromOwner} FromPlatform={FromPlatform} at {At}",
                refund.Id, payment.Id, ownerId, reverseFromOwner, reverseFromPlatform, DateTimeOffset.UtcNow);
        }

        var journalId = Guid.NewGuid().ToString("N");
        await _ledger.WriteJournalAsync(
            journalId,
            LedgerReferenceTypes.Refund,
            refund.Id.ToString(),
            payment.Id,
            [
                new LedgerLine(AccountType.Platform, null, refundPlatformFee, IsDebit: true,
                    Description: $"Refund #{refund.Id} — hoàn phí nền tảng"),
                new LedgerLine(AccountType.Tax, null, refundTax, IsDebit: true,
                    Description: $"Refund #{refund.Id} — hoàn thuế GTGT"),
                .. refundPersonalIncomeTax > 0m
                    ? new LedgerLine[]
                    {
                        new(AccountType.PersonalIncomeTax, null, refundPersonalIncomeTax, IsDebit: true,
                            Description: $"Refund #{refund.Id} — hoàn thuế TNCN")
                    }
                    : [],
                .. ownerShareLines,
                new LedgerLine(AccountType.Gateway, null, amountApproved, IsDebit: false,
                    Description: $"Refund #{refund.Id} — hoàn tiền qua cổng thanh toán")
            ], ct);

        refund.Status = RefundRequestStatus.Approved;
        refund.AmountApproved = amountApproved;
        refundRepo.Update(refund);

        // The settlement tranches for this payment were sized against its original gross amount;
        // shrink whichever ones haven't released yet by the same proportion, or
        // SettlementReleaseJob will still pay the owner for a ticket that was refunded.
        var settlementRepo = _uow.Repository<Settlement, int>();
        //
        // MLACP-335: phai gom ca PendingReview, khong chi Scheduled. Mot tranche bi chot D16 giu
        // lai VAN CHUA chi tra dong nao — no chi dang doi Admin quyet. Bo sot no o day nghia la neu
        // Admin sau do bam chi tra, phong tra nhan du tien cho ca phan da hoan cho khach.
        var pendingSettlements = await settlementRepo.FindAsync(
            s => s.PaymentId == payment.Id
                 && (s.Status == SettlementStatus.Scheduled || s.Status == SettlementStatus.PendingReview), ct);
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
