namespace MusicLounge.Application.Common.Interfaces;

// General-purpose Gemini text call, distinct from IAiModerationService (which parses its response
// into a fixed AiModerationResult shape) — this returns the raw response text so callers with their
// own JSON shape (e.g. per-recommendation explanations) can parse it themselves. The prompt must
// ask for JSON, since the underlying call always requests Gemini's JSON output mode.
//
// Fail-open, like IAiModerationService and unlike IAiImageGenerationService: every current caller
// uses this to enrich an already-complete feature (e.g. add a nicer explanation string) rather than
// to perform the user's requested action, so a null result here must never break anything — the
// caller keeps whatever non-AI fallback it already had.
public interface IAiTextGenerationService
{
    Task<string?> GenerateJsonAsync(string prompt, CancellationToken ct = default);
}
