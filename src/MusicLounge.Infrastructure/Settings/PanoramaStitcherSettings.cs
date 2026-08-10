namespace MusicLounge.Infrastructure.Settings;

// Deployment-target/infra config, same category as GeminiSettings/OpenAiSettings — not a
// system_config business number. Points at the standalone panorama-stitcher microservice
// (services/panorama-stitcher/), which is deliberately NOT part of this .NET process — see that
// service's README for why (OS-pinned native OpenCV binaries on Linux, isolated to its own
// deployable unit instead of this backend's).
public sealed class PanoramaStitcherSettings
{
    public string BaseUrl { get; init; } = string.Empty;

    // The panorama-stitcher is a separate process (possibly a separate host) that fetches source
    // images over plain HTTP - it needs a publicly-fetchable absolute URL, not the relative
    // "/uploads/xxx.jpg" IFileStorageService returns. This is ONLY prepended to URLs the
    // validator has already confirmed start with "/uploads/" (see
    // StitchVenueTourSceneCommandValidator) - never to an arbitrary caller-supplied URL, which
    // would defeat the point (SSRF: a malicious Owner could otherwise point the stitcher at
    // internal network addresses or cloud metadata endpoints).
    public string PublicBaseUrl { get; init; } = string.Empty;
}
