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
    private readonly INotificationService _notifications;
    private readonly ILogger<ProcessRefundRequestCommandHandler> _logger;

    public ProcessRefundRequestCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILedgerService ledger,
        IPaymentRepository paymentRepo,
        IVnPayService vnPay,
        IAsyncKeyedLock @lock,
        ISystemConfigService config,
        INotificationService notifications,
        ILogger<ProcessRefundRequestCommandHandler> logger)
    {
        _uow = uow;
        _currentUser = currentUser;
        _ledger = ledger;
        _paymentRepo = paymentRepo;
        _vnPay = vnPay;
        _lock = @lock;
        _config = config;
        _notifications = notifications;
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

        // MLACP-348: AutoApproveOverdueRefundsJob gui lenh nay tu mot job nen, khong co ai dang nhap.
        // CurrentUserService.UserId nem loi khi khong co HttpContext — co y, de mot hanh dong khong
        // ro ai lam thi hong han chu khong ghi cho mot "User 0" khong ton tai. Chi co AutoApproved
        // moi duoc bo qua buoc doc do; duong Admin duyet van doc UserId, van fail-closed nhu cu.
        // ProcessedBy = null nghia la he thong tu duyet.
        int? actorId = request.AutoApproved ? null : _currentUser.UserId;
        refund.ProcessedBy = actorId;
        refund.ResolvedAt = DateTimeOffset.UtcNow;
        refund.ResolutionNote = string.IsNullOrWhiteSpace(request.ResolutionNote)
            ? null
            : request.ResolutionNote.Trim();

        if (request.Decision == "Rejected")
        {
            refund.Status = RefundRequestStatus.Rejected;
            refundRepo.Update(refund);

            // MLACP-337. Truoc day handler nay khong bao cho ai ca — nguoi mua gui yeu cau roi
            // phai tu di hoi. Dieu 31 Luat BVQLNTD 2023 noi dung ve nghia vu thong bao cho nguoi
            // tieu dung ket qua xu ly khieu nai.
            // MLACP-342. Truong ly do la TUY CHON tren API — bat buoc se lam vo hop dong dang
            // duoc dung. Nen khi khong co ly do, thong bao van phai huu ich: chi cho nguoi mua
            // duong khieu nai de duoc xem lai, thay vi bo ho lai voi mot chu "bi tu choi".
            await NotifyBuyerAsync(
                refund,
                "Yeu cau hoan tien khong duoc chap nhan",
                refund.ResolutionNote is { } why
                    ? $"Yeu cau hoan tien cua ban khong duoc chap nhan. Ly do: {why}. Neu ban khong " +
                      "dong y, hay gui khieu nai de duoc xem xet lai."
                    : "Yeu cau hoan tien cua ban da duoc xem xet va khong duoc chap nhan. Neu ban " +
                      "khong dong y, hay gui khieu nai de duoc xem xet lai.",
                ct);

            await _uow.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Refund request rejected: RefundRequestId={RefundRequestId} PaymentId={PaymentId} by AdminUserId={AdminUserId} at {At}",
                refund.Id, refund.PaymentId, actorId, DateTimeOffset.UtcNow);

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

        // MLACP-337. Cho nay truoc day goi VNPay vo dieu kien. Ve ban tai quay
        // (SellWalkInTicket) co Method = Cash, TransactionId null, va OrderId chua bao gio duoc
        // gui sang VNPay — nen lenh goi that bai, handler nem ExternalServiceException, va yeu
        // cau hoan tien nam Pending vinh vien. Nguoi mua ve tai quay khong co duong nao duoc
        // hoan tien qua he thong.
        var isGatewayPayment = payment.Method == PaymentMethod.Gateway;

        if (isGatewayPayment)
        {
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
                CreatedBy: actorId?.ToString() ?? "system",
                IpAddress: request.ClientIpAddress), ct);

            if (!vnPayResult.IsSuccess)
                throw new ExternalServiceException(
                    "VNPay",
                    $"Gọi API hoàn tiền VNPay thất bại (mã lỗi {vnPayResult.ResponseCode}): {vnPayResult.Message}. " +
                    "Yêu cầu hoàn tiền vẫn ở trạng thái Pending, chưa ghi sổ cái.");
        }
        else
        {
            _logger.LogInformation(
                "Hoan tien ve ban tai quay — nen tang khong giu khoan nay nen khong co lenh hoan " +
                "qua cong thanh toan. RefundRequestId={RefundRequestId} PaymentId={PaymentId} " +
                "OwnerId={OwnerId} SoTien={Amount} at {At}",
                refund.Id, payment.Id, ownerId, amountApproved, DateTimeOffset.UtcNow);
        }

        // MLACP-337. Ve tien mat khong ghi so cai — WriteTicketLedgerHandler bo qua Method == Cash
        // khi WalkInCommissionEnabled tat, va no mac dinh tat; chu thich tai do noi ro "Cash
        // payments never actually flow through the platform's own accounts". Ghi mot but toan dao
        // cho giao dich nhu vay se tao ra nhung dong khong doi ung voi gi ca, trong do co mot dong
        // co AccountType.Gateway cho mot giao dich chua bao gio di qua cong thanh toan nao. So cai
        // chi ghi them chu khong sua duoc, nen phai chan tu dau.
        //
        // Kiem but toan co THAT SU ton tai, khong soi lai co WalkInCommissionEnabled: co la ban sao
        // thu ba cua mot quy tac da co hai ban sao, va no co the bi bat len giua luc ban ve va luc
        // hoan tien — luc do soi co se di dao nhung but toan chua bao gio duoc ghi.
        // Duong cong thanh toan luon co but toan mua (WriteTicketLedgerHandler ghi ngay khi xac
        // nhan), nen ve nhanh o day va giu nguyen y hanh vi cu cho no. Phep do that su chi can cho
        // nhanh tien mat: no CO the co but toan neu WalkInCommissionEnabled duoc bat, va co do co
        // the doi giua luc ban ve va luc hoan tien — nen doc but toan that thay vi soi lai co.
        var shouldReverseJournal = isGatewayPayment
            || await _uow.Repository<LedgerEntry, int>().AnyAsync(e => e.PaymentId == payment.Id, ct);

        // Ti le nay dung cho ca hai viec: dao but toan (chi khi co but toan de dao) va co gian cac
        // tranche quyet toan chua giai ngan (luon chay). Nen no nam ngoai khoi duoi.
        var ratio = amountApproved / payment.GrossAmount;

        if (shouldReverseJournal)
        {
        // Proportional reversal of the original purchase journal (D8 — reverse via offsetting
        // lines, never mutate the original). Owner's share is the remainder rather than its own
        // rounded ratio so debit/credit balance exactly regardless of rounding.
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
        }

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

        // MLACP-337. Noi dung khac nhau theo duong tien, va cho nao noi that cho do: voi ve mua
        // online thi nen tang da thuc su phat lenh hoan; voi ve mua tai quay thi nen tang chua bao
        // gio giu khoan do nen KHONG duoc hua thay phong tra.
        if (isGatewayPayment)
        {
            await NotifyBuyerAsync(
                refund,
                "Yeu cau hoan tien da duoc duyet",
                $"{amountApproved:N0}d se duoc hoan ve phuong thuc thanh toan ban da dung. Thoi gian " +
                "tien ve tai khoan phu thuoc ngan hang phat hanh.",
                ct);
        }
        else
        {
            await NotifyBuyerAsync(
                refund,
                "Yeu cau hoan tien da duoc duyet",
                $"{amountApproved:N0}d se duoc phong tra hoan truc tiep cho ban, vi ve nay duoc mua " +
                "tai quay. Chung toi da thong bao cho phong tra. Neu chua nhan duoc, hay gui khieu nai.",
                ct);

            await _notifications.NotifyAsync(
                ownerId,
                NotificationType.RefundOwedByVenue,
                "Can hoan tien mat cho khach",
                $"Ve #{refund.PaymentId} duoc mua tai quay bang tien mat nen nen tang khong giu khoan " +
                $"nay. Phong tra can hoan {amountApproved:N0}d truc tiep cho khach.",
                referenceType: "refund",
                referenceId: refund.Id.ToString(),
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Refund request approved: RefundRequestId={RefundRequestId} PaymentId={PaymentId} AmountApproved={AmountApproved} by AdminUserId={AdminUserId} at {At}",
            refund.Id, refund.PaymentId, amountApproved, actorId, DateTimeOffset.UtcNow);

        return Unit.Value;
    }
    /// <summary>
    /// Bao cho nguoi mua ma yeu cau nay danh cho. <c>RequestedBy</c> nullable vi khoa ngoai dat
    /// <c>SET NULL</c> khi tai khoan bi xoa theo luat bao ve du lieu ca nhan — luc do khong con ai
    /// de bao, nen bo qua la dung.
    /// </summary>
    private async Task NotifyBuyerAsync(
        RefundRequest refund, string title, string body, CancellationToken ct)
    {
        if (refund.RequestedBy is not int buyerId) return;

        await _notifications.NotifyAsync(
            buyerId,
            NotificationType.RefundUpdate,
            title,
            body,
            referenceType: "refund",
            referenceId: refund.Id.ToString(),
            ct: ct);
    }
}
