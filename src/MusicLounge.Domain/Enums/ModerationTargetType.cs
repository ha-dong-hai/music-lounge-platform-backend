namespace MusicLounge.Domain.Enums;

public enum ModerationTargetType
{
    Show,
    Livestream,
    GalleryImage,
    TourScene,

    // MLACP-388: hang ve livestream them sau khi buoi dien da dang — gia chi mo ban khi Admin duyet.
    TicketTier
}
