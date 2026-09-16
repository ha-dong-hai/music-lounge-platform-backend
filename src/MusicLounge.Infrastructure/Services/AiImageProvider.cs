using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-418: quy tac chon nha cung cap tao anh, tach rieng de test duoc ma khong phai dung ca DI container.
/// Uu tien Cloudflare Workers AI vi co bac mien phi; OpenAI la duong du phong (phai nap tien moi dung duoc).
/// </summary>
public static class AiImageProvider
{
    public static bool UseCloudflare(CloudflareSettings settings)
        => !string.IsNullOrWhiteSpace(settings.AccountId) && !string.IsNullOrWhiteSpace(settings.ApiToken);

    /// <summary>
    /// MLACP-424: mat xich CHUA CAU HINH phai bi loai khoi chuoi, khong duoc de vao roi tinh la mot lan thu hong.
    /// Neu de vao: Cloudflare het han muc (429) roi OpenAI nem "Chua cau hinh OpenAI API key", loi cau hinh do la
    /// loi cuoi cung nen se de mat thong bao "het luot trong ngay" — chu phong tra nhan dung mot cau vo nghia
    /// voi ho va che mat ly do that.
    /// </summary>
    public static bool UseOpenAi(OpenAiSettings settings) => !string.IsNullOrWhiteSpace(settings.ApiKey);

    /// <summary>
    /// MLACP-424: danh sach model Cloudflare se lam mat xich, theo thu tu uu tien. Chua cau hinh Cloudflare thi tra
    /// ve rong — de ben goi khong dung nham mot mat xich chac chan hong.
    /// </summary>
    public static IReadOnlyList<string> CloudflareModels(CloudflareSettings settings)
    {
        if (!UseCloudflare(settings)) return [];

        var danhSach = settings.ImageModels
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .ToList();
        if (danhSach.Count > 0) return danhSach;

        return [string.IsNullOrWhiteSpace(settings.ImageModel)
            ? CloudflareImageGenerationService.DefaultModel
            : settings.ImageModel.Trim()];
    }
}
