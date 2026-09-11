namespace MusicLounge.Application.Livestreams.DTOs;

/// <param name="Message">Null khi người donate không để công khai, lời nhắn dính từ cấm, hoặc đã bị
/// phòng trà gỡ (MLACP-360).</param>
/// <param name="DonationId">MLACP-360 — để client gỡ đúng lời nhắn khi nhận sự kiện
/// <c>DonationMessageHidden</c>.</param>
public sealed record DonationAlertDto(
    string DonorName,
    decimal Amount,
    string? Message,
    int DonationId);
