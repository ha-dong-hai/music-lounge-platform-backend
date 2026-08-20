namespace MusicLounge.Application.Common.Interfaces;

// Shared block-or-flag decision logic for every Owner-uploaded image that isn't already covered
// by the text-based EventModeration flow (show/livestream) — currently venue tour scenes and
// gallery photos. One gate instead of duplicating the threshold logic at each of those three call
// sites (AddVenueTourScene, StitchVenueTourScene, AddLoungeGalleryImage).
public interface IImageModerationGate
{
    /// <summary>
    /// Throws DomainException if the image scores at/above the block threshold (an Owner should
    /// never even see this image land in their tour/gallery). Returns the raw AiModerationResult
    /// if it scores at/above the (lower) review threshold — the caller should create an
    /// EventModeration row for it once the new entity's id is known. Returns null if the image is
    /// clean, or if scoring was unavailable (fail-open: vendor down/unconfigured never blocks an
    /// upload — same philosophy as the existing text moderation).
    /// </summary>
    Task<AiModerationResult?> CheckOrThrowAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
