using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Donations.Commands.ProcessDonationPayment;

internal sealed class ProcessDonationPaymentCommandHandler
    : IRequestHandler<ProcessDonationPaymentCommand, VnPayIpnOutcome>
{
    private readonly IUnitOfWork _uow;
    private readonly IVnPayService _vnPay;
    private readonly IDonationRepository _donationRepo;
    private readonly INotificationService _notifications;
    private readonly ILedgerService _ledger;
    private readonly ISystemConfigService _config;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<ProcessDonationPaymentCommandHandler> _logger;

    public ProcessDonationPaymentCommandHandler(
        IUnitOfWork uow, IVnPayService vnPay, IDonationRepository donationRepo, INotificationService notifications,
        ILedgerService ledger, ISystemConfigService config, IAsyncKeyedLock @lock,
        ILogger<ProcessDonationPaymentCommandHandler> logger)
    {
        _uow = uow;
        _vnPay = vnPay;
        _donationRepo = donationRepo;
        _notifications = notifications;
        _ledger = ledger;
        _config = config;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<VnPayIpnOutcome> Handle(ProcessDonationPaymentCommand request, CancellationToken ct)
    {
        var callbackResult = _vnPay.VerifyCallback(request.QueryParams);

        // Reject tampered/forged callbacks immediately — do NOT modify any data
        if (!callbackResult.IsSignatureValid)
        {
            request.QueryParams.TryGetValue("vnp_TxnRef", out var rejectedTxnRef);
            _logger.LogWarning(
                "VNPay donation callback rejected: invalid signature. TxnRef={TxnRef}", rejectedTxnRef);
            return VnPayIpnOutcome.InvalidSignature;
        }

        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRef);

        // VNPay retries the IPN callback for the same txnRef for up to ~15 minutes if it doesn't
        // get back the exact {RspCode,Message} it expects — without this lock, 2 near-simultaneous
        // callbacks both read Status==PendingPayment before either commits, and both write a
        // ledger journal for the same donation (double revenue). Keyed by txnRef so the lock is
        // held before the row is even looked up.
        await using var _ = await _lock.AcquireAsync($"vnpay-donation:{txnRef}", ct);

        var donations = await _uow.Repository<Donation, int>()
            .FindAsync(d => d.GatewayRef == txnRef, ct);

        var donation = donations.FirstOrDefault();
        if (donation is null) return VnPayIpnOutcome.OrderNotFound;

        // Idempotency: only process if still in initial state.
        // A duplicate callback arriving after Owner already acknowledged would wrongly cancel.
        if (donation.Status != DonationStatus.PendingPayment)
        {
            // MLACP-334. Truoc day ca hai tinh huong duoi day gop chung mot ket qua, nen cai thu
            // hai - tien that da thu - bien mat khong dau vet.
            if (callbackResult.IsSuccess && donation.Status == DonationStatus.Cancelled)
            {
                await PaymentIncident.RecordConfirmedTooLateAsync(
                    _uow, _notifications, _logger, "donate", txnRef, callbackResult.Amount,
                    "donation", donation.Id.ToString(), ct);
                return VnPayIpnOutcome.ConfirmedTooLate;
            }

            return VnPayIpnOutcome.AlreadyProcessed;
        }

        // Signature only proves the callback came from VNPay, not that it's for the amount WE
        // expect — a same-day forged/replayed txnRef with a different vnp_Amount would otherwise
        // still pass. Fail closed (treat as unconfirmed) on mismatch rather than trust it.
        if (callbackResult.IsSuccess && callbackResult.Amount != donation.Gross)
        {
            _logger.LogWarning(
                "VNPay donation callback amount mismatch: DonationId={DonationId} Expected={Expected} Actual={Actual}",
                donation.Id, donation.Gross, callbackResult.Amount);
            return VnPayIpnOutcome.AmountMismatch;
        }

        if (callbackResult.IsSuccess)
        {
            // VNPay confirmed payment — move to PendingOwnerAck (Owner must still acknowledge)
            donation.Status = DonationStatus.PendingOwnerAck;
            donation.PaymentConfirmedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            // VNPay reported failure — cancel donation so it doesn't stay stuck
            donation.Status = DonationStatus.Cancelled;
            _logger.LogWarning(
                "VNPay donation payment failed: DonationId={DonationId} ResponseCode={ResponseCode}",
                donation.Id, callbackResult.ResponseCode);
        }

        _uow.Repository<Donation, int>().Update(donation);

        if (callbackResult.IsSuccess)
        {
            var ownership = await _donationRepo.GetOwnershipInfoAsync(donation.Id, ct);
            if (ownership is { } info)
            {
                // Chặng 1 (§6.5) — "tức thì": the platform/tax cut and the owner's share are
                // recorded the instant VNPay confirms payment, not deferred like ticket
                // settlement. Previously this whole flow (create → VNPay confirm → owner ack →
                // owner-paid-performer) never wrote a single ledger entry, so donation revenue,
                // platform commission, and tax withheld were completely invisible to the ledger
                // and to GetLedgerIntegrityQueryHandler — a real gap against BR-19/NĐ 117.
                var commissionRate = await _config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
                var taxes = await TaxWithholdingPolicy.ResolveForOwnerAsync(
                    _uow, _config, info.OwnerId, ct);
                var fees = PaymentFeeCalculator.Split(
                    donation.Gross, commissionRate, taxes.VatRate, taxes.PersonalIncomeTaxRate);

                // CreateDonationCommandHandler freezes an ESTIMATED Net using whatever rate was
                // configured at donation time — if an admin changes PlatformCommissionRate/TaxRate
                // before VNPay confirms (minutes to hours later), that estimate no longer matches
                // fees.OwnerNet just credited to the ledger below. ConfirmDonationPaidCommandHandler
                // (chặng 2) later debits donation.Net verbatim, so it MUST equal what was credited
                // here or the two legs permanently drift. This is the single point where the real
                // rate is known — overwrite Net here so credit (chặng 1) and debit (chặng 2) always
                // agree, mirroring how WriteTicketLedgerHandler snapshots payment.NetAmount once at
                // confirmation instead of trusting a value computed earlier.
                donation.Net = fees.OwnerNet;

                // Same snapshot-at-confirmation reasoning as Net above, for the chặng-2 split rate:
                // this is the one moment the donation is confirmed real, so it's also the right
                // moment to freeze what the performer was promised. ConfirmDonationPaidCommandHandler
                // reads THIS column (falling back to a live config read only for pre-migration
                // donations that never got a snapshot) instead of re-reading system_config at
                // whatever arbitrary later moment the Owner gets around to confirming payout — which
                // could be days/weeks after this, per DonationHoldDays — so an Admin rate change in
                // between can't retroactively change what a performer receives for a donation that
                // already happened.
                donation.PerformerShareRateSnapshot =
                    await _config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);

                _uow.Repository<Donation, int>().Update(donation);

                await _ledger.WriteJournalAsync(
                    Guid.NewGuid().ToString("N"),
                    LedgerReferenceTypes.Donation,
                    donation.Id.ToString(),
                    paymentId: null,
                    [
                        new(AccountType.Gateway, null, donation.Gross, IsDebit: true),
                        new(AccountType.Platform, null, fees.PlatformFee, IsDebit: false,
                            Description: "Hoa hồng nền tảng"),
                        new(AccountType.Tax, null, fees.Tax, IsDebit: false,
                            Description: "Thuế GTGT khấu trừ tại nguồn"),
                        .. fees.PersonalIncomeTax > 0m
                            ? new LedgerLine[]
                            {
                                new(AccountType.PersonalIncomeTax, null, fees.PersonalIncomeTax,
                                    IsDebit: false, Description: "Thuế TNCN khấu trừ tại nguồn")
                            }
                            : [],
                        new(AccountType.User, info.OwnerId, fees.OwnerNet, IsDebit: false,
                            Description: $"Donate #{donation.Id} — chặng 1, nhận ngay")
                    ], ct);

                await _notifications.NotifyAsync(
                    info.OwnerId,
                    NotificationType.DonationReceived,
                    "Bạn vừa nhận donate!",
                    $"Có donate {donation.Gross:N0}đ đang chờ bạn xác nhận đã nhận tiền.",
                    referenceType: "donation",
                    referenceId: donation.Id.ToString(),
                    ct: ct);

                _logger.LogInformation(
                    "Donation payment confirmed: DonationId={DonationId} Gross={Gross} OwnerNet={OwnerNet} OwnerId={OwnerId}",
                    donation.Id, donation.Gross, fees.OwnerNet, info.OwnerId);
            }
        }

        await _uow.SaveChangesAsync(ct);

        return callbackResult.IsSuccess ? VnPayIpnOutcome.Confirmed : VnPayIpnOutcome.RecordedAsFailed;
    }
}
