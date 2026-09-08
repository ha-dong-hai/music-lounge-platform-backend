using MusicLounge.Application.Common.Abstractions;

namespace MusicLounge.Application.Users.Queries.GetMyTaxProfile;

public sealed record GetMyTaxProfileQuery : IQuery<TaxProfileDto>;

/// <param name="TaxCode">
/// Returned in full to its owner — it is their own tax code, and a masked value would make the
/// screen useless for checking a typo, which is the main reason to look at it.
/// </param>
/// <param name="WithholdingApplies">
/// Whether this platform currently deducts tax from what the seller earns. The single question the
/// screen exists to answer, computed by the same policy the ledger uses rather than left for the
/// client to infer from the fields above.
/// </param>
public sealed record TaxProfileDto(
    string? BusinessType,
    string? TaxCode,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? VerifiedAt,
    string? ReviewStatus,
    /// <summary>Why it was turned down. The only thing that makes a rejection actionable.</summary>
    string? ReviewNote,
    bool WithholdingApplies,
    decimal VatRate,
    decimal PersonalIncomeTaxRate,
    string Explanation);
