using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

// Mux signature spec (docs.mux.com/core/verify-webhook-signatures):
//   header:  Mux-Signature: t=<unix-seconds>,v1=<hex-hmac-sha256>
//   signed payload: "{t}.{rawBody}"
//   algorithm: HMAC-SHA256 keyed with the Mux webhook signing secret
// Timestamp tolerance mirrors Mux's own SDKs (5 minutes) to reject replayed old requests while
// tolerating normal clock/network skew.
public sealed class MuxWebhookVerifier : IMuxWebhookVerifier
{
    private static readonly TimeSpan ToleranceWindow = TimeSpan.FromMinutes(5);

    private readonly MuxSettings _settings;

    public MuxWebhookVerifier(IOptions<MuxSettings> settings) => _settings = settings.Value;

    public bool VerifySignature(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        string? timestampPart = null;
        string? signaturePart = null;
        foreach (var segment in signatureHeader.Split(','))
        {
            var kv = segment.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t") timestampPart = kv[1];
            else if (kv[0] == "v1") signaturePart = kv[1];
        }

        if (timestampPart is null || signaturePart is null) return false;
        if (!long.TryParse(timestampPart, out var timestampSeconds)) return false;

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if (DateTimeOffset.UtcNow - timestamp > ToleranceWindow || timestamp - DateTimeOffset.UtcNow > ToleranceWindow)
            return false;

        var signedPayload = $"{timestampPart}.{rawBody}";
        var key = Encoding.UTF8.GetBytes(_settings.WebhookSecret);
        var computedHash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(signedPayload));
        var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(signaturePart));
    }
}
