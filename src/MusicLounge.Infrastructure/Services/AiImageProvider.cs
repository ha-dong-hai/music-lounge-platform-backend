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

    /// <summary>
    /// MLACP-480: sinh anh bang Gemini. Doi DU CA HAI — co khoa VA co khai model anh. Chi co khoa thi KHONG du: khoa
    /// Gemini dung chung voi kiem duyet noi dung, ma kiem duyet chay duoc tren bac mien phi con sinh anh thi khong
    /// (bac mien phi tra limit: 0 cho ca bon model anh). Suy ra "co khoa la bat sinh anh" se khien moi moi truong
    /// chi cau hinh kiem duyet lang le chuyen sang mot nha cung cap luon that bai.
    /// </summary>
    public static bool UseGemini(GeminiSettings settings)
        => !string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ImageModel);
}
