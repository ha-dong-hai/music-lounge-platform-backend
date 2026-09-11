namespace MusicLounge.Application.Donations.DTOs;

/// <summary>
/// MLACP-365 — tổng hợp sao kê công khai của một nghệ sĩ. Trừ <see cref="TotalGross"/>, mọi số tiền là
/// <b>phần của nghệ sĩ</b>, chia theo nơi tiền đang nằm. Khoản không công khai số tiền (chỉ có ở dữ liệu
/// cũ) không được cộng vào — <see cref="DonationsWithHiddenAmount"/> cho biết có bao nhiêu khoản như vậy.
/// </summary>
public sealed record PerformerDonationSummaryDto(
    int PerformerId,
    string PerformerName,
    int DonationCount,
    int DonationsWithHiddenAmount,
    decimal TotalGross,
    decimal TotalForPerformer,
    decimal HeldByPlatform,
    decimal HeldByVenue,
    decimal OverdueAtVenue,
    int OverdueCount,
    decimal ReportedPaidToPerformer,
    decimal ConfirmedByPerformer,
    decimal DisputedByPerformer,
    int PaidLateCount,
    PublicDonationPolicyDto Policy
);

/// <summary>MLACP-365 — chính sách donate đang áp dụng cho khoản mới, công bố cùng sao kê.</summary>
public sealed record PublicDonationPolicyDto(
    decimal PerformerShareRate,
    decimal PlatformCommissionRate,
    int VenuePayoutDays,
    int VenueWarningDays,
    bool Refundable,
    bool AmountAlwaysPublic,
    IReadOnlyList<string> Statements
);
