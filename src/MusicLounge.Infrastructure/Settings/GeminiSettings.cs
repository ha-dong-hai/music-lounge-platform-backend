namespace MusicLounge.Infrastructure.Settings;

public sealed class GeminiSettings
{
    public string ApiKey { get; init; } = string.Empty;

    // Model id, not a business-tunable number — belongs alongside the other vendor settings
    // (Mux/Cloudflare tokens) in appsettings, not system_config. Overridable via config if Google
    // deprecates this model later, without needing a code change.
    public string Model { get; init; } = "gemini-3.6-flash";

    /// <summary>
    /// Model sinh ẢNH. Để TRỐNG là không dùng Gemini để tạo poster — và mặc định phải là trống.
    ///
    /// <para>MLACP-480. Vì sao tách khỏi <see cref="ApiKey"/> thay vì "có khoá thì dùng luôn": khoá Gemini dùng CHUNG
    /// cho kiểm duyệt nội dung, mà kiểm duyệt (sinh văn bản) CHẠY ĐƯỢC trên bậc miễn phí còn sinh ảnh thì KHÔNG —
    /// bậc miễn phí trả <c>limit: 0</c> cho cả bốn model ảnh (đo thật 09/08 và 16/09/2026). Nếu cứ có khoá là bật
    /// sinh ảnh thì mọi môi trường chỉ cấu hình kiểm duyệt sẽ lặng lẽ chuyển sang một nhà cung cấp luôn thất bại.
    /// Một khoá cấu hình riêng buộc người vận hành nói rõ "project này ĐÃ bật thanh toán".</para>
    ///
    /// <para>Đã đo 21/09/2026 sau khi bật thanh toán: <c>gemini-3.1-flash-image</c>, khổ 3:4, 15,4 giây, ảnh JPEG
    /// ~738 KB, không chữ, không dấu nhìn thấy được.</para>
    /// </summary>
    public string ImageModel { get; init; } = string.Empty;
}
