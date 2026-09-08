namespace MusicLounge.Domain.Entities;

public sealed class DeviceToken : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }

    public User User { get; set; } = null!;
}
