namespace MusicLounge.Application.Performers.DTOs;

/// <summary>
/// MLACP-364 — nghệ sĩ thấy gì khi mở liên kết: đang được hỏi xác nhận điều gì, với đúng những con số
/// phòng trà đã khai.
/// </summary>
/// <param name="State">Open | Used | Expired | Outdated (tài khoản đã bị sửa sau khi gửi liên kết).</param>
public sealed record PerformerConfirmationDto(
    string Purpose,
    string PerformerName,
    DateTimeOffset ExpiresAt,
    string State,
    string? Outcome,
    string? BankName,
    string? AccountNumberMasked,
    string? AccountHolder,
    decimal? Amount,
    string? PaymentRef,
    string? ShowName,
    string? VenueName);
