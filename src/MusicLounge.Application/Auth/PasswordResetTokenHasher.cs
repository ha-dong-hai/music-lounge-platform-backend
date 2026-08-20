using System.Security.Cryptography;
using System.Text;

namespace MusicLounge.Application.Auth;

/// <summary>
/// Token thô gửi qua email không bao giờ lưu trong DB — chỉ lưu SHA256 hex của nó, giống nguyên
/// tắc "không lưu bí mật dạng đọc được" dùng cho session token nói chung.
/// </summary>
internal static class PasswordResetTokenHasher
{
    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
