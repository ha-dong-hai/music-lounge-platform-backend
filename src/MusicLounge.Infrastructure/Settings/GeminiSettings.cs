namespace MusicLounge.Infrastructure.Settings;

public sealed class GeminiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // Model id, not a business-tunable number — belongs alongside the other vendor settings
    // (Mux/Cloudflare tokens) in appsettings, not system_config. Overridable via config if Google
    // deprecates this model later, without needing a code change.
    public string Model { get; init; } = "gemini-3.6-flash";

    // Text scoring only — image generation moved to OpenAI (see OpenAiSettings/
    // OpenAiImageGenerationService) since the Gemini API has no free tier at all for image models
    // and the user chose to switch vendors rather than enable Google Cloud billing.
}
