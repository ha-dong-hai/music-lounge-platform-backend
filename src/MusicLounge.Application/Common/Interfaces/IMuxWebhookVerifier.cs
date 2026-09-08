namespace MusicLounge.Application.Common.Interfaces;

/// <summary>Verifies the `Mux-Signature` header Mux attaches to every webhook call, per Mux's
/// official spec: header is `t=&lt;unix-seconds&gt;,v1=&lt;hex-hmac-sha256&gt;`, signed payload is
/// `"{t}.{rawBody}"`. Needs the RAW request body bytes (before any JSON model binding reformats
/// them) — a re-serialized body would produce a different signature and always fail verification.</summary>
public interface IMuxWebhookVerifier
{
    bool VerifySignature(string rawBody, string? signatureHeader);
}
