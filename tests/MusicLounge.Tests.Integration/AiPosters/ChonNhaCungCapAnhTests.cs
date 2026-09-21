using FluentAssertions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-480. Thứ tự chọn nhà cung cấp tạo ảnh, tách riêng để kiểm được mà không phải dựng cả DI container.
///
/// <para>Thứ tự: hàng đợi máy trạm (lựa chọn CÓ Ý của người vận hành) → Gemini → Cloudflare → OpenAI.</para>
/// </summary>
public sealed class ChonNhaCungCapAnhTests
{
    private static bool DungGemini(string khoa, string model)
        => LayQuyTac("UseGemini", new GeminiSettings { ApiKey = khoa, ImageModel = model });

    private static bool LayQuyTac(string ten, object cauHinh)
    {
        var kieu = typeof(CloudflareSettings).Assembly.GetType("MusicLounge.Infrastructure.Services.AiImageProvider")!;
        return (bool)kieu.GetMethod(ten)!.Invoke(null, [cauHinh])!;
    }

    [Fact]
    public void ChiCoKhoaMaKhongKhaiModelAnh_THI_KHONG_DungGemini()
    {
        // Đây là ca quan trọng nhất. Khoá Gemini dùng CHUNG với kiểm duyệt nội dung, và kiểm duyệt chạy được trên bậc
        // miễn phí còn sinh ảnh thì KHÔNG (bậc miễn phí trả limit: 0 cho cả bốn model ảnh). Nếu "có khoá là bật sinh
        // ảnh" thì mọi môi trường chỉ cấu hình kiểm duyệt sẽ lặng lẽ chuyển sang một nhà cung cấp luôn thất bại.
        DungGemini("khoa-kiem-duyet", "").Should().BeFalse();
    }

    [Fact]
    public void ChiKhaiModelAnhMaKhongCoKhoa_THI_KHONG_DungGemini()
        => DungGemini("", "gemini-3.1-flash-image").Should().BeFalse();

    [Fact]
    public void CoDuCaHai_THI_DungGemini()
        => DungGemini("khoa-123", "gemini-3.1-flash-image").Should().BeTrue();

    [Fact]
    public void KhoaChiCoKhoangTrang_KhongTinhLaCoKhoa()
        => DungGemini("   ", "gemini-3.1-flash-image").Should().BeFalse();
}
