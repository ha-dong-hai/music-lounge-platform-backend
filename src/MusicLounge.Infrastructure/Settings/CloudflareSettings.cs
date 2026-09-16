namespace MusicLounge.Infrastructure.Settings;

public sealed class CloudflareSettings
{
    public string ApiToken { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;

    /// <summary>MLACP-418: de trong = FLUX.1 schnell (mien phi, ~230 anh/ngay). Doi model khi can chat luong khac.</summary>
    public string ImageModel { get; init; } = string.Empty;
}
