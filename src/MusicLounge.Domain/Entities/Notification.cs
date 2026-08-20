using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public sealed class Notification : Common.BaseEntity<int>
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }      // deep link: "show", "ticket", "settlement"
    public string? ReferenceId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTimeOffset? SentAt { get; set; }     // null = pending FCM delivery
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
