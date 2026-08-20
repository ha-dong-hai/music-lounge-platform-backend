namespace MusicLounge.Application.Common.Interfaces;

// Unlike IAiModerationService, this is NOT fail-open — a poster generation call is a direct,
// user-visible action the Owner explicitly requested, so a failure must surface as a real error
// (caught by GeneratePosterCommandHandler and logged as a Failed AiPosterGeneration row that does
// NOT count against the Owner's monthly quota), not silently swallowed.
public interface IAiImageGenerationService
{
    Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default);
}
