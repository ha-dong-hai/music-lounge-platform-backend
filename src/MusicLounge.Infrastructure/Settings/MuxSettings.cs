namespace MusicLounge.Infrastructure.Settings;

public sealed class MuxSettings
{
    public string TokenId { get; init; } = string.Empty;
    public string TokenSecret { get; init; } = string.Empty;

    // Mux Dashboard > Settings > Webhooks — used to verify the Mux-Signature header on inbound
    // webhook calls, separate from TokenId/TokenSecret (those authenticate OUR calls to Mux's API).
    public string WebhookSecret { get; init; } = string.Empty;
}
