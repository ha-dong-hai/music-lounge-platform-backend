using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.DomainEventHandlers;

internal sealed class ScheduleSettlementHandler : INotificationHandler<TicketPaymentConfirmed>
{
    private readonly IUnitOfWork _uow;
    private readonly ISystemConfigService _config;

    public ScheduleSettlementHandler(IUnitOfWork uow, ISystemConfigService config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task Handle(TicketPaymentConfirmed notification, CancellationToken ct)
    {
        var payment = await _uow.Repository<Payment, int>().GetByIdAsync(notification.PaymentId, ct);
        if (payment is null || notification.OwnerId == 0) return;

        // Cash (walk-in/box-office) sales must not get a settlement schedule by default — the owner
        // already holds 100% of the cash from the moment of sale, so a scheduled payout here would be
        // a real second (duplicate) bank transfer for money the platform never collected. Same
        // ConfigKeys.WalkInCommissionEnabled gate as WriteTicketLedgerHandler — keep both in sync,
        // since a ledger journal with no matching settlement (or vice versa) breaks the invariant
        // that payment.NetAmount always equals what the owner is actually scheduled to receive.
        if (payment.Method == PaymentMethod.Cash
            && !await _config.GetBoolAsync(ConfigKeys.WalkInCommissionEnabled, false, ct))
            return;

        var commissionRate = await _config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
        var taxRate = await _config.GetDecimalAsync(ConfigKeys.TaxRate, 0.05m, ct);
        var partialPct = await _config.GetDecimalAsync(ConfigKeys.SettlementPartialPct, 0.70m, ct);

        var gross = payment.GrossAmount;
        // Same split WriteTicketLedgerHandler uses for payment.NetAmount — otherwise the two
        // handlers can round to different owner-net figures for the same payment, and this
        // settlement's stage1+stage2 silently stops matching what the ledger/payment record says
        // the owner is owed.
        var ownerNet = PaymentFeeCalculator.Split(gross, commissionRate, taxRate).OwnerNet;

        // Schedule payout: fetch show end from tickets
        var ticket = await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => t.PaymentId == payment.Id, ct);
        var showId = ticket.FirstOrDefault()?.ShowId;

        var showEnd = showId.HasValue
            ? await ResolveShowEndAsync(showId.Value, ct)
            : DateTimeOffset.UtcNow.AddDays(3);

        // D3: 2-stage settlement — partial at T+48h, remainder at T+14d (ratio from system_config)
        var stage1Amount = Math.Round(ownerNet * partialPct, 2);
        var stage2Amount = ownerNet - stage1Amount;

        var repo = _uow.Repository<Settlement, int>();
        // D12: snapshot rates at time of creation — config changes later won't affect existing settlements
        var preRate = partialPct;
        var postRate = 1 - partialPct;

        repo.Add(new Settlement
        {
            OwnerId = notification.OwnerId,
            PaymentId = payment.Id,
            ReleaseType = SettlementReleaseType.Partial70,
            GrossAmount = gross,
            PreRateApplied = preRate,
            PostRateApplied = postRate,
            NetAmount = stage1Amount,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = showEnd.AddHours(48),
            CreatedAt = DateTimeOffset.UtcNow
        });
        repo.Add(new Settlement
        {
            OwnerId = notification.OwnerId,
            PaymentId = payment.Id,
            ReleaseType = SettlementReleaseType.Final30,
            GrossAmount = gross,
            PreRateApplied = preRate,
            PostRateApplied = postRate,
            NetAmount = stage2Amount,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = showEnd.AddDays(14),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
    }

    private async Task<DateTimeOffset> ResolveShowEndAsync(int showId, CancellationToken ct)
    {
        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(showId, ct);
        return show?.ScheduledEnd ?? show?.ScheduledStart.AddHours(4) ?? DateTimeOffset.UtcNow;
    }
}
