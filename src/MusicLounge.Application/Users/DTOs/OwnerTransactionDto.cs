namespace MusicLounge.Application.Users.DTOs;

public sealed record OwnerTransactionDto(
    Guid Id,
    string Type,
    string ReferenceId,
    decimal Amount,
    string? Description,
    DateTimeOffset CreatedAt)
{
    // MLACP-655: câu tiếng Việt cho chủ phòng trà đọc ("Tiền vé đợt 70% — Đêm tình ca Trịnh · 2 vé").
    // Description là chú thích NỘI BỘ của sổ cái ("Settlement #<guid> payout", "Donate #<guid> — chặng 2…") — giữ
    // nguyên cho đối soát, nhưng giao diện hiện Title. Không đặt vào tham số vị trí để các chỗ đang dựng DTO bằng
    // constructor (LedgerEntryRepository, PR mở #409) không phải đổi.
    public string? Title { get; init; }
}
