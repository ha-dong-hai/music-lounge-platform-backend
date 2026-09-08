namespace MusicLounge.Domain.Enums;

/// <summary>
/// What kind of taxpayer a seller is, which decides whether this platform withholds tax on their
/// behalf at all.
///
/// NĐ 117/2025/NĐ-CP puts the withholding duty on platforms that handle payment, but only for
/// households and individuals doing business through them. An enterprise declares and pays its own
/// VAT and corporate income tax, so withholding from one would take money the platform has no
/// standing to take and the enterprise no easy way to reclaim.
/// </summary>
public enum PayeeBusinessType
{
    /// <summary>Hộ kinh doanh hoặc cá nhân kinh doanh — the platform withholds on their behalf.</summary>
    HouseholdOrIndividual,

    /// <summary>Doanh nghiệp — declares and pays its own tax; the platform withholds nothing.</summary>
    Enterprise
}
