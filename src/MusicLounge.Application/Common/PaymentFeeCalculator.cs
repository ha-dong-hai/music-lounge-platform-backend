namespace MusicLounge.Application.Common;

/// <summary>
/// Single source of truth for splitting a ticket payment's gross amount into platform fee, tax,
/// and owner net. WriteTicketLedgerHandler (writes payment.NetAmount + the ledger journal) and
/// ScheduleSettlementHandler (creates the Settlement rows that actually get paid out) used to
/// compute this independently — one rounded platformFee and tax separately then subtracted from
/// gross, the other rounded gross*(1-totalRate) in one step. Rounding is not distributive over
/// addition, so the two owner-net figures were not guaranteed to agree to the cent for arbitrary
/// rates/amounts, even though each was individually "balanced" within its own handler. Both now
/// call this instead, so payment.NetAmount and settlement stage1+stage2 can never drift apart.
/// </summary>
public static class PaymentFeeCalculator
{
    /// <param name="taxRate">Thuế GTGT. 5% for services under NĐ 117/2025.</param>
    /// <param name="personalIncomeTaxRate">
    /// Thuế TNCN, 2% for a resident individual. Pass 0 for a seller the platform does not withhold
    /// for — see <see cref="TaxWithholdingPolicy"/>, which is what decides that.
    /// </param>
    public static PaymentFeeBreakdown Split(
        decimal grossAmount,
        decimal platformCommissionRate,
        decimal taxRate,
        decimal personalIncomeTaxRate)
    {
        var platformFee = Math.Round(grossAmount * platformCommissionRate, 2);
        var tax = Math.Round(grossAmount * taxRate, 2);
        // Each withheld amount is a percentage of gross in its own right, not of what is left after
        // the previous deduction — the decree sets both rates against doanh thu của giao dịch.
        var personalIncomeTax = Math.Round(grossAmount * personalIncomeTaxRate, 2);
        // Owner net is defined as the remainder, not independently rounded — guarantees
        // platformFee + tax + personalIncomeTax + ownerNet == grossAmount exactly, which the
        // ledger's debit-must-equal-credit invariant depends on.
        var ownerNet = grossAmount - platformFee - tax - personalIncomeTax;
        return new PaymentFeeBreakdown(platformFee, tax, personalIncomeTax, ownerNet);
    }

    /// <summary>
    /// Donate chặng 2 (§6.5): owner forwards <paramref name="performerShareRate"/> of the ORIGINAL
    /// gross to the performer, keeping the rest of their chặng-1 net as compensation for holding/
    /// administering the donation. Deliberately takes <paramref name="ownerNet"/> (chặng 1's
    /// already-committed figure, per WriteTicketLedgerHandler's snapshot-at-commitment-point
    /// pattern) rather than re-deriving it — if commission/tax rates changed between chặng 1 and
    /// chặng 2, this must still balance against what was actually credited, not what today's config
    /// would produce. Same "remainder, not independently rounded" rule as Split above.
    /// </summary>
    public static DonationPayoutSplit SplitDonationPayout(
        decimal grossAmount, decimal ownerNet, decimal performerShareRate)
    {
        var performerAmount = Math.Round(grossAmount * performerShareRate, 2);
        var ownerRetained = ownerNet - performerAmount;
        return new DonationPayoutSplit(performerAmount, ownerRetained);
    }
}

public sealed record PaymentFeeBreakdown(
    decimal PlatformFee, decimal Tax, decimal PersonalIncomeTax, decimal OwnerNet);

/// <summary>
/// <paramref name="OwnerRetained"/> negative means <c>performerShareRate</c> is misconfigured
/// (asks the owner to forward more than they actually received in chặng 1) — callers must check
/// this before writing a ledger journal, since the ledger cannot represent a negative transfer.
/// </summary>
public sealed record DonationPayoutSplit(decimal PerformerAmount, decimal OwnerRetained);
