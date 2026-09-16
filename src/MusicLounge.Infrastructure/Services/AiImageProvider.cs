using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-418: quy tac chon nha cung cap tao anh, tach rieng de test duoc ma khong phai dung ca DI container.
/// Uu tien Cloudflare Workers AI vi co bac mien phi; OpenAI la duong du phong (phai nap tien moi dung duoc).
/// </summary>
internal static class AiImageProvider
{
    public static bool UseCloudflare(CloudflareSettings settings)
        => !string.IsNullOrWhiteSpace(settings.AccountId) && !string.IsNullOrWhiteSpace(settings.ApiToken);
}
