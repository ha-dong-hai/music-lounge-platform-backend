// CoreFlow: CF2 (Event Discovery), CF3 (Ticket Booking), CF4 (Livestream), CF5 (Interaction), CF6 (Payment)
// Push notification record sent to a user via FCM.
// ReferenceType + ReferenceId are used by the mobile app to deep-link to the relevant screen.
using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class Notification : BaseEntity<int>
{
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public bool IsRead { get; set; } = false;
    // Null = queued but not yet delivered to FCM
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
