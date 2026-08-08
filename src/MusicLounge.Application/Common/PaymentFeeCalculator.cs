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
    public static PaymentFeeBreakdown Split(decimal grossAmount, decimal platformCommissionRate, decimal taxRate)
    {
        var platformFee = Math.Round(grossAmount * platformCommissionRate, 2);
        var tax = Math.Round(grossAmount * taxRate, 2);
        // Owner net is defined as the remainder, not independently rounded — guarantees
        // platformFee + tax + ownerNet == grossAmount exactly, which the ledger's
        // debit-must-equal-credit invariant depends on.
        var ownerNet = grossAmount - platformFee - tax;
        return new PaymentFeeBreakdown(platformFee, tax, ownerNet);
    }
}

public sealed record PaymentFeeBreakdown(decimal PlatformFee, decimal Tax, decimal OwnerNet);
