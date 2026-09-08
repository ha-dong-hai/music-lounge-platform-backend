using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Commands.SubmitTaxProfile;

/// <param name="BusinessType">
/// "HouseholdOrIndividual" (hộ/cá nhân kinh doanh) or "Enterprise" (doanh nghiệp).
/// </param>
/// <param name="TaxCode">
/// Mã số thuế: 10 digits for a taxpayer, or 13 with a 3-digit branch suffix. NĐ 117/2025 asks the
/// platform to hold either this or the seller's personal identification number; the citizen card
/// already covers the second, so this completes the pair rather than duplicating it.
/// </param>
public sealed record SubmitTaxProfileCommand(string BusinessType, string TaxCode) : ICommand;
