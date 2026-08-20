using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DomainEventHandlers;

internal sealed class WriteTicketLedgerHandler : INotificationHandler<TicketPaymentConfirmed>
{
    private readonly IUnitOfWork _uow;
    private readonly ILedgerService _ledger;
    private readonly ISystemConfigService _config;

    public WriteTicketLedgerHandler(IUnitOfWork uow, ILedgerService ledger, ISystemConfigService config)
    {
        _uow = uow;
        _ledger = ledger;
        _config = config;
    }

    public async Task Handle(TicketPaymentConfirmed notification, CancellationToken ct)
    {
        var payment = await _uow.Repository<Payment, int>().GetByIdAsync(notification.PaymentId, ct);
        if (payment is null) return;

        // Cash (walk-in/box-office) payments never actually flow through the platform's own
        // gateway account — the venue's own staff collect the cash directly. Writing a Gateway-debit
        // journal here would falsely claim the platform received money it never touched, and
        // (combined with ScheduleSettlementHandler below) would schedule a REAL bank payout of the
        // owner's share for cash the owner already has in hand — paying them twice. Off by default
        // (product decision 2026-08-09) — see ConfigKeys.WalkInCommissionEnabled for what turning
        // this on does and does not cover.
        if (payment.Method == PaymentMethod.Cash
            && !await _config.GetBoolAsync(ConfigKeys.WalkInCommissionEnabled, false, ct))
            return;

        var commissionRate = await _config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
        var taxRate = await _config.GetDecimalAsync(ConfigKeys.TaxRate, 0.05m, ct);

        var journalId = Guid.NewGuid().ToString("N");
        var gross = payment.GrossAmount;
        var fees = PaymentFeeCalculator.Split(gross, commissionRate, taxRate);

        // Populate fee breakdown on payment (H1 — snapshot at confirmation time)
        payment.PlatformFee = fees.PlatformFee;
        payment.TaxWithheld = fees.Tax;
        payment.NetAmount = fees.OwnerNet;
        _uow.Repository<Payment, int>().Update(payment);

        // Owner's share is credited to Platform (held in trust), NOT to the owner's own User
        // account — that account is only credited later, when SettlementReleaseJob actually
        // pays out the 70% / 30% tranches (D3/§6.6). Crediting User here as well as there would
        // pay every owner double: once immediately for the full amount, then again for the 70%
        // and 30% settlement tranches derived from that same amount. Verified 2026-08-05: no
        // other query reads AccountType.User for an owner before settlement release, so nothing
        // depends on the old (incorrect) immediate-credit behavior.
        await _ledger.WriteJournalAsync(
            journalId,
            LedgerReferenceTypes.Payment,
            payment.Id.ToString(),
            payment.Id,
            new LedgerLine[]
            {
                new(AccountType.Gateway, null, gross, IsDebit: true),
                new(AccountType.Platform, null, fees.PlatformFee, IsDebit: false,
                    Description: "Hoa hồng nền tảng"),
                new(AccountType.Platform, null, fees.OwnerNet, IsDebit: false,
                    Description: $"Giữ hộ chủ phòng trà #{notification.OwnerId} — chờ settlement"),
                new(AccountType.Tax, null, fees.Tax, IsDebit: false)
            }, ct);

        await _uow.SaveChangesAsync(ct);
    }
}
