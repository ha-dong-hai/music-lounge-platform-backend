using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Common;

/// <summary>
/// Decides which tax rates this platform withholds from a given seller's revenue.
///
/// Until now the answer was "the same one rate, from everybody". NĐ 117/2025/NĐ-CP makes it two
/// questions instead: which taxes, and for whom.
///
/// WHICH TAXES. The decree has a payment-handling platform withhold both VAT and personal income
/// tax on each successful transaction (Điều 5 fixes the moment of withholding as the point the
/// transaction is confirmed and payment accepted — which is exactly where WriteTicketLedgerHandler
/// already runs). The system had only ever withheld one rate, 5%, and that figure happens to be the
/// VAT rate for services; personal income tax, 2% for a resident individual, was missing entirely
/// rather than folded in.
///
/// FOR WHOM. Only households and individuals. An enterprise seller declares its own tax, so
/// withholding from one is simply taking money that is not the platform's to take.
///
/// The default when the seller has not been classified is to withhold, because that is what the
/// system does today and because the recoverable mistake is the safer one: an over-withheld
/// enterprise is a support ticket, whereas an under-withheld household is an unpaid tax liability
/// nobody is tracking.
/// </summary>
public static class TaxWithholdingPolicy
{
    /// <param name="payeeBusinessType">
    /// Null means the seller has not been classified yet — treated as household/individual.
    /// </param>
    /// <param name="isVerified">
    /// Whether the classification has actually been checked by the platform. An unverified claim of
    /// being an enterprise is a self-service instruction to stop withholding tax, which is not
    /// something a seller gets to assert about themselves.
    /// </param>
    public static TaxWithholdingRates Resolve(
        PayeeBusinessType? payeeBusinessType,
        bool isVerified,
        decimal vatRate,
        decimal personalIncomeTaxRate)
        => payeeBusinessType == PayeeBusinessType.Enterprise && isVerified
            ? new TaxWithholdingRates(0m, 0m)
            : new TaxWithholdingRates(vatRate, personalIncomeTaxRate);

    /// <summary>
    /// Looks the seller up and reads the configured rates, so the three places that withhold money —
    /// the ticket ledger journal, the settlement schedule and the donation journal — cannot end up
    /// answering the question differently for the same payment. The ticket path in particular writes
    /// the journal in one handler and sizes the payout in another, and those two disagreeing is the
    /// exact failure PaymentFeeCalculator was extracted to prevent.
    /// </summary>
    public static async Task<TaxWithholdingRates> ResolveForOwnerAsync(
        IUnitOfWork uow, ISystemConfigService config, int ownerId, CancellationToken ct)
    {
        var owner = await uow.Repository<User, int>().GetByIdAsync(ownerId, ct);
        var vatRate = await config.GetDecimalAsync(ConfigKeys.TaxRate, 0.05m, ct);
        // Defaulted to 0 rather than to the decree's 2%: switching this on changes what every
        // household seller is paid, so it is an explicit decision an Admin makes through
        // PUT /admin/system-config, with a mandatory reason recorded, not something that starts
        // happening because a deployment went out.
        var personalIncomeTaxRate = await config.GetDecimalAsync(ConfigKeys.PersonalIncomeTaxRate, 0m, ct);

        return Resolve(
            owner?.BusinessType,
            isVerified: owner?.TaxProfileVerifiedAt is not null,
            vatRate,
            personalIncomeTaxRate);
    }
}

/// <param name="VatRate">Thuế GTGT — 5% for services under NĐ 117/2025.</param>
/// <param name="PersonalIncomeTaxRate">Thuế TNCN — 2% for services from a resident individual.</param>
public sealed record TaxWithholdingRates(decimal VatRate, decimal PersonalIncomeTaxRate);
