namespace MusicLounge.Application.Common.Interfaces;

// Same fail-open contract as IAiModerationService (text) — null means "vendor down/unconfigured/
// malformed response", not a decision. The caller (IImageModerationGate) decides what a null
// result means for the upload; this service only ever reports what the vendor actually said.
public interface IImageModerationService
{
    Task<AiModerationResult?> CheckAsync(byte[] imageBytes, string mimeType, CancellationToken ct = default);
}
