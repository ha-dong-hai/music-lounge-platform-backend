namespace MusicLounge.Infrastructure.Settings;

// Deployment-target/infra config, same category as GeminiSettings/OpenAiSettings — not a
// system_config business number. Points at the standalone panorama-stitcher microservice
// (services/panorama-stitcher/), which is deliberately NOT part of this .NET process — see that
// service's README for why (OS-pinned native OpenCV binaries on Linux, isolated to its own
// deployable unit instead of this backend's).
public sealed class PanoramaStitcherSettings
{
    public string BaseUrl { get; init; } = string.Empty;
}
