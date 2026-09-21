using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Infrastructure.Services;

/// <summary>
/// MLACP-418: quy tac chon nha cung cap tao anh, tach rieng de test duoc ma khong phai dung ca DI container.
/// Uu tien Cloudflare Workers AI vi co bac mien phi; OpenAI la duong du phong (phai nap tien moi dung duoc).
/// </summary>
internal enum NhaCungCapAnh
{
    Gemini,
    HangDoiMayTram,
    Cloudflare,
    OpenAi
}

internal static class AiImageProvider
{
    /// <summary>
    /// MLACP-481: TOAN BO thu tu chon nha cung cap nam o day, mot cho, de kiem duoc.
    ///
    /// <para>Truoc day thu tu nam rai trong mot bieu thuc ba ngoi long nhau o DependencyInjection — moi cong bat deu
    /// co test rieng, nhung THU TU giua chung thi khong ai kiem. Ma thu tu moi la thu quyet dinh anh cua chu phong tra
    /// do ai sinh ra.</para>
    ///
    /// <para>Gemini dung DAU (chu du an chot 21/09/2026): goi thang tu may chu, ~15 giay, khong can ai truc, va anh
    /// KHONG co dau nhin thay duoc. Hang doi may tram (Google Flow) tuy mien phi nhung doi mot may co Chrome dang dang
    /// nhap, token het han moi gio, va Flow in ngoi sao 4 canh cua Google len anh.</para>
    ///
    /// <para>HE QUA can biet: da khai Gemini:ImageModel thi may tram se KHONG BAO GIO nhan duoc don nua. Muon quay ve
    /// dung may tram thi phai BO TRONG Gemini:ImageModel.</para>
    /// </summary>
    public static NhaCungCapAnh Chon(
        GeminiSettings gemini, PosterWorkerSettings mayTram, CloudflareSettings cloudflare)
        => UseGemini(gemini) ? NhaCungCapAnh.Gemini
            : UseDeferredQueue(mayTram) ? NhaCungCapAnh.HangDoiMayTram
            : UseCloudflare(cloudflare) ? NhaCungCapAnh.Cloudflare
            : NhaCungCapAnh.OpenAi;

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
