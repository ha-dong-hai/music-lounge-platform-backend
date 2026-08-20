namespace MusicLounge.Infrastructure.Settings;

public sealed class CloudflareSettings
{
    public string ApiToken { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
}
