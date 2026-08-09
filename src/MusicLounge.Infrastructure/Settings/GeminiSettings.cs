namespace MusicLounge.Infrastructure.Settings;

public sealed class GeminiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // Model id, not a business-tunable number — belongs alongside the other vendor settings
    // (Mux/Cloudflare tokens) in appsettings, not system_config. Overridable via config if Google
    // deprecates this model later, without needing a code change.
    public string Model { get; init; } = "gemini-3.6-flash";

    // Separate model id for image generation — a different capability from text scoring, priced
    // and rate-limited independently. Defaults to the cheaper "Nano Banana" flash-image tier
    // (~$0.04-0.13/image) rather than the "Pro" tier (~$0.13-0.24/image) since a social-media-quality
    // poster doesn't need the Pro tier's higher resolution ceiling — override here if that changes.
    public string ImageModel { get; init; } = "gemini-2.5-flash-image";
}
