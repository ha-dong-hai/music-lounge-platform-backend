using MusicLounge.Domain.Common;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Domain.Entities;

public class UserBehaviourLog : BaseEntity<int>
{
    public int UserId { get; set; }
    // Null for actions not tied to a specific show (e.g. search_genre)
    public int? ShowId { get; set; }
    public BehaviourAction Action { get; set; }
    // Null for instant actions (click, wishlist); set for time-based actions (watch_livestream, view_event_long)
    public int? DurationSeconds { get; set; }
    // Additional context: scroll depth, source page, etc. Stored as JSON string
    public string? Metadata { get; set; }
    // Only written when users.ai_consent = true (BVDLCN 2025)
    public DateTime CreatedAt { get; set; }
}
