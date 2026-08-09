using MediatR;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;
using MusicLoungeEntity = MusicLounge.Domain.Entities.MusicLounge;

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

        // Schedule payout: fetch show + lounge from tickets — needed for both the D3 payout-speed
        // tier (venue reputation) and the payout bank account (which venue's account to pay into).
        var ticket = await _uow.Repository<Ticket, Guid>()
            .FindAsync(t => t.PaymentId == payment.Id, ct);
        var showId = ticket.FirstOrDefault()?.ShowId;
        var show = showId.HasValue
            ? await _uow.Repository<LoungeShow, int>().GetByIdAsync(showId.Value, ct)
            : null;
        var lounge = show is not null
            ? await _uow.Repository<MusicLoungeEntity, int>().GetByIdAsync(show.LoungeId, ct)
            : null;

        // Bank account is a hard prerequisite, not an optional nicety — a settlement with nowhere to
        // pay into is the exact half-wired-feature bug this fixes (Settlement.BankAccountId used to
        // be defined and snapshotted-in-comment but never actually assigned anywhere). Fail closed:
        // an Owner who hasn't registered a default payout account yet must not silently accumulate
        // scheduled settlements with no destination.
        var bankAccountId = lounge is not null
            ? await ResolveDefaultBankAccountIdAsync(BankAccountOwnerType.Lounge, lounge.Id, ct)
            : null;
        if (bankAccountId is null)
        {
            throw new DomainException(
                "Không thể lên lịch thanh toán: venue chưa đăng ký tài khoản ngân hàng mặc định. " +
                "Vui lòng thêm tài khoản ngân hàng trước khi tiếp tục bán vé.");
        }

        var commissionRate = await _config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
        var taxRate = await _config.GetDecimalAsync(ConfigKeys.TaxRate, 0.05m, ct);

        var gross = payment.GrossAmount;
        // Same split WriteTicketLedgerHandler uses for payment.NetAmount — otherwise the two
        // handlers can round to different owner-net figures for the same payment, and this
        // settlement's stage1+stage2 silently stops matching what the ledger/payment record says
        // the owner is owed.
        var ownerNet = PaymentFeeCalculator.Split(gross, commissionRate, taxRate).OwnerNet;

        // D3: payout-speed tier by venue standing — rewards a well-reviewed, established venue with
        // a larger up-front tranche instead of everyone getting the same flat rate. Computed live
        // from ratings/show-count rather than trusting the cached MusicLounge.ReputationScore column
        // (that column is written nowhere in this codebase, so it would always read 0 and put every
        // venue in Tier Mới regardless of actual standing) — and written back below so the cache
        // stops being permanently stale for anything that does display it.
        var partialPct = lounge is not null
            ? await ResolveTierPreRateAsync(lounge, ct)
            : await _config.GetDecimalAsync(ConfigKeys.SettlementTierNewPreRate, 0.50m, ct);

        var showEnd = show is not null
            ? (show.ScheduledEnd ?? show.ScheduledStart.AddHours(4))
            : DateTimeOffset.UtcNow.AddDays(3);

        var partialHoursAfterShow = await _config.GetIntAsync(ConfigKeys.SettlementPartialHoursAfterShow, 48, ct);
        var finalDaysAfterShow = await _config.GetIntAsync(ConfigKeys.SettlementFinalDaysAfterShow, 14, ct);

        // D3: 2-stage settlement — partial at T+partialHoursAfterShow, remainder at T+finalDaysAfterShow
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
            BankAccountId = bankAccountId,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = showEnd.AddHours(partialHoursAfterShow),
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
            BankAccountId = bankAccountId,
            Status = SettlementStatus.Scheduled,
            ScheduledAt = showEnd.AddDays(finalDaysAfterShow),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _uow.SaveChangesAsync(ct);
    }

    private async Task<int?> ResolveDefaultBankAccountIdAsync(
        BankAccountOwnerType ownerType, int ownerId, CancellationToken ct)
    {
        var accounts = await _uow.Repository<BankAccount, int>().FindAsync(
            a => a.OwnerType == ownerType && a.OwnerId == ownerId && a.IsDefault, ct);
        return accounts.FirstOrDefault()?.Id;
    }

    private async Task<decimal> ResolveTierPreRateAsync(MusicLoungeEntity lounge, CancellationToken ct)
    {
        var ratings = await _uow.Repository<LoungeShowRating, int>().FindAsync(
            r => !r.IsRemoved && r.LoungeShow.LoungeId == lounge.Id, ct);
        var score = ratings.Count > 0 ? (decimal)ratings.Average(r => r.Score) : 0m;

        var completedShows = await _uow.Repository<LoungeShow, int>().CountAsync(
            s => s.LoungeId == lounge.Id && s.Status == LoungeShowStatus.Ended, ct);

        var standardMinScore = await _config.GetDecimalAsync(ConfigKeys.SettlementTierStandardMinScore, 3.5m, ct);
        var premiumMinScore = await _config.GetDecimalAsync(ConfigKeys.SettlementTierPremiumMinScore, 4.2m, ct);
        var premiumMinShows = await _config.GetIntAsync(ConfigKeys.SettlementTierPremiumMinShows, 10, ct);

        // Keep the display-facing cached score in sync now that it's actually being computed —
        // it was never written anywhere before this handler, so it always read 0 regardless of a
        // venue's real standing.
        if (lounge.ReputationScore != score)
        {
            lounge.ReputationScore = score;
            _uow.Repository<MusicLoungeEntity, int>().Update(lounge);
        }

        if (score >= premiumMinScore && completedShows >= premiumMinShows)
            return await _config.GetDecimalAsync(ConfigKeys.SettlementTierPremiumPreRate, 0.80m, ct);
        if (score >= standardMinScore)
            return await _config.GetDecimalAsync(ConfigKeys.SettlementTierStandardPreRate, 0.70m, ct);
        return await _config.GetDecimalAsync(ConfigKeys.SettlementTierNewPreRate, 0.50m, ct);
    }
}
