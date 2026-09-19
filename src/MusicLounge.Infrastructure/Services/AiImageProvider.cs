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

    /// <summary>
    /// MLACP-458: chế độ hàng đợi (máy trạm chạy Google Flow). Đòi ĐỦ CẢ HAI — bật cờ và có khoá — vì bật mà quên khoá thì
    /// ba endpoint <c>/poster-jobs</c> sẽ không ai gọi được, và để hệ thống nhận đơn vào một hàng đợi không bao giờ có
    /// người lấy là tệ hơn việc quay về nhà cung cấp gọi thẳng.
    /// </summary>
    public static bool UseDeferredQueue(PosterWorkerSettings settings)
        => settings.Enabled && !string.IsNullOrWhiteSpace(settings.ApiKey);
}
