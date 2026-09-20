namespace MusicLounge.Application.Donations.DTOs;

public sealed record MyDonationDto(
    int Id,
    // Tên nghệ sĩ để hiển thị; Id để bấm vào xem trang nghệ sĩ hoặc ủng hộ tiếp. Trước đây chỉ có
    // tên, mà tên thì không dò ngược ra người được.
    int PerformerId,
    string PerformerName,
    string ShowName,
    decimal Gross,
    string Status,
    bool IsAnonymous,
    string? Message,
    DateTimeOffset? PaymentConfirmedAt,
    DateTimeOffset CreatedAt);
