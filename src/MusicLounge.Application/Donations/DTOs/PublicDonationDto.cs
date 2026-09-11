namespace MusicLounge.Application.Donations.DTOs;

/// <summary>
/// Một dòng sao kê công khai của khoản donate cho nghệ sĩ (D17; mở rộng ở MLACP-365, chỉ thêm trường).
/// Không bao giờ chứa số tài khoản, mã chuyển khoản, mã giao dịch hay ảnh chứng từ — xem
/// <see cref="PublicDonationStatement"/>.
/// </summary>
public sealed record PublicDonationDto(
    int Id,
    string ShowName,
    string VenueName,
    DateTimeOffset ShowDate,
    string? DonorDisplayName,           // null = ẩn danh
    decimal? Gross,
    string Status,
    DateTimeOffset CreatedAt,
    // ── MLACP-365 ──
    DateTimeOffset? PaidAt,             // VNPay xác nhận thanh toán
    string? Message,                    // chỉ khi công khai, chưa bị gỡ, không chứa từ cấm
    decimal? PlatformFee,               // null: donate có từ trước khi có bản ghi thanh toán
    decimal? TaxWithheld,               // GTGT + TNCN khấu trừ tại nguồn
    decimal? PerformerAmount,
    decimal? VenueRetained,
    DateTimeOffset? PlatformPaidVenueAt,
    DateTimeOffset? VenueAcknowledgedAt,
    bool VenueAcknowledgedAutomatically,
    DateTimeOffset? PayoutDueAt,        // hạn phòng trà chuyển cho nghệ sĩ
    DateTimeOffset? VenueReportedPaidAt,
    bool HasTransferReceipt,            // có chứng từ lưu trữ (bản thân chứng từ không công khai)
    bool Overdue,                       // chưa báo chuyển và đã quá hạn
    bool PaidLate,                      // báo chuyển sau hạn
    bool PerformerAskedToConfirm,
    string? PerformerResponse,          // Confirmed | Disputed | null
    DateTimeOffset? PerformerRespondedAt,
    string Stage,                       // PlatformHolding | VenueHolding | VenueReportedPaid | PerformerConfirmed | PerformerDisputed
    string StageLabel
);
