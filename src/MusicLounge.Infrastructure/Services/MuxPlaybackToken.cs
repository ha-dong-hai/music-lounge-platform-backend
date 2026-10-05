using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-647. Token xem cho luồng Mux có playback policy "signed": JWT ký RS256 bằng signing key của Mux.
/// Theo tài liệu Mux "Secure video playback": header <c>kid</c> = signing key id; claims <c>sub</c> = playback id,
/// <c>aud</c> = "v" (video), <c>exp</c> = giây Unix. Khoá riêng Mux cấp ở dạng PEM đã mã hoá base64.
/// Tự dựng thay vì thêm thư viện JWT: ba trường cố định, và repo hạn chế thêm dependency (CLAUDE.md bậc 5).
/// </summary>
public static class MuxPlaybackToken
{
    public static string Create(string signingKeyId, string base64PrivateKeyPem, string playbackId, DateTimeOffset expiresAt)
    {
        var header = new { alg = "RS256", typ = "JWT", kid = signingKeyId };
        var payload = new { sub = playbackId, aud = "v", exp = expiresAt.ToUnixTimeSeconds() };
        var unsigned = $"{B64(JsonSerializer.SerializeToUtf8Bytes(header))}.{B64(JsonSerializer.SerializeToUtf8Bytes(payload))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(Encoding.UTF8.GetString(Convert.FromBase64String(base64PrivateKeyPem)));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{unsigned}.{B64(signature)}";
    }

    private static string B64(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
