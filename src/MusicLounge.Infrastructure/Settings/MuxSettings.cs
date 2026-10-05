namespace MusicLounge.Infrastructure.Settings;

public sealed class MuxSettings
{
    public string TokenId { get; init; } = string.Empty;
    public string TokenSecret { get; init; } = string.Empty;

    // Mux Dashboard > Settings > Webhooks — used to verify the Mux-Signature header on inbound
    // webhook calls, separate from TokenId/TokenSecret (those authenticate OUR calls to Mux's API).
    public string WebhookSecret { get; init; } = string.Empty;

    // MLACP-647: Mux Dashboard > Settings > Signing Keys. Có cả hai thì buổi phát có phí dùng link xem có chữ ký
    // (MuxStreamService). SigningKeyPrivate là khoá riêng Mux cấp (PEM đã mã hoá base64) — bí mật, chỉ đặt qua biến môi
    // trường / Key Vault, không ghi vào appsettings trong repo.
    public string SigningKeyId { get; init; } = string.Empty;
    public string SigningKeyPrivate { get; init; } = string.Empty;
}
