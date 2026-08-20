namespace MusicLounge.Domain.Entities;

public sealed class LivestreamChatMessage : Common.BaseEntity<int>
{
    public int LivestreamId { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }

    public Livestream Livestream { get; set; } = null!;
    public User User { get; set; } = null!;
}
