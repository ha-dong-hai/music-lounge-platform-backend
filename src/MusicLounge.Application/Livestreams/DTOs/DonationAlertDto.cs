namespace MusicLounge.Application.Livestreams.DTOs;

/// <param name="Message">Null khi người donate không để công khai, lời nhắn dính từ cấm, hoặc đã bị
/// phòng trà gỡ (MLACP-360).</param>
/// <param name="DonationId">MLACP-360 — để client gỡ đúng lời nhắn khi nhận sự kiện
/// <c>DonationMessageHidden</c>.</param>
/// <param name="PerformerName">
/// MLACP-451: nghệ sĩ nhận lượt donate này. Mỗi lượt donate gắn với một tiết mục cụ thể (tiền được chuyển cho đúng nghệ
/// sĩ đó), nhưng thông báo trên sóng trước đây không nói ai nhận — buổi hòa nhạc có nhiều nghệ sĩ thì khán giả lẫn người
/// trên sân khấu không biết lượt donate dành cho ai. <c>null</c> khi không tra được (thông báo vẫn phát).
/// </param>
public sealed record DonationAlertDto(
    string DonorName,
    decimal Amount,
    string? Message,
    int DonationId,
    string? PerformerName = null);
