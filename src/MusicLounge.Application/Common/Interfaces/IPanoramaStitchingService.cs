namespace MusicLounge.Application.Common.Interfaces;

// Not fail-open — like IAiImageGenerationService, a stitch call is a direct, user-visible action
// the Owner explicitly requested, so a failure must surface as a real error (caught by
// StitchVenueTourSceneCommandHandler and logged as a Failed VenueTourStitchAttempt row that does
// NOT count against the Owner's MaxTourScenes quota), not silently swallowed.
public interface IPanoramaStitchingService
{
    Task<byte[]> StitchAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default);
}
