namespace MusicLounge.Domain.Entities;

public sealed class LivestreamChatMessage : Common.BaseEntity<int>
{
    public int LivestreamId { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }

    // MLACP-456: go theo bao cao vi pham cua nguoi dung (ND 147/2024). Go MEM chu khong xoa cung — giu lai de doi
    // chieu ve sau, giong cach LoungeShowRating.IsRemoved dang lam. Tin nhan da go bi loc khoi lich su chat.
    public bool IsRemoved { get; set; }
    public string? RemovedReason { get; set; }

    public Livestream Livestream { get; set; } = null!;
    public User User { get; set; } = null!;
}
