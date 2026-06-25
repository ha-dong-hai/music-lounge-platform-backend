// CoreFlow: CF1 (Event Management), CF4 (Livestream Participation)
// What is being reviewed by Admin in the moderation queue.
namespace MusicLounge.Domain.Enums;

public enum ShowModerationTargetType
{
    // A show submission waiting for Admin approval before publishing
    Show = 1,
    // An active livestream being monitored for policy violations (NĐ 147/2024)
    Livestream = 2
}
