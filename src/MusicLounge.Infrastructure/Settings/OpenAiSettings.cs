namespace MusicLounge.Infrastructure.Settings;

public sealed class OpenAiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // gpt-image-1.5, not gpt-image-1 (retiring 2026-10-23 — don't build new integrations on it) or
    // gpt-image-2/-mini's newer async generate-then-poll flow, which this simple synchronous
    // integration doesn't need. Override here if pricing/availability changes again.
    public string Model { get; init; } = "gpt-image-1.5";
}
