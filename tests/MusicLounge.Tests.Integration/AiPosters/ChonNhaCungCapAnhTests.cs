using FluentAssertions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// Thứ tự chọn nhà cung cấp tạo ảnh nền poster.
///
/// <para>MLACP-481: <b>Gemini đứng đầu</b> (chủ dự án chốt 21/09/2026), rồi mới tới hàng đợi máy trạm (Google Flow),
/// Cloudflare, OpenAI. Gemini gọi thẳng từ máy chủ, ~15 giây, không cần ai trực, và ảnh không có dấu nhìn thấy được;
/// Flow tuy miễn phí nhưng đòi một máy có Chrome đang đăng nhập và in ngôi sao 4 cánh của Google lên ảnh.</para>
///
/// <para>Trước đây thứ tự nằm rải trong một biểu thức ba ngôi lồng nhau ở <c>DependencyInjection</c>: mỗi cổng bật
/// đều có phép kiểm riêng, nhưng THỨ TỰ giữa chúng thì không ai kiểm — mà thứ tự mới là thứ quyết định ảnh của chủ
/// phòng trà do ai sinh ra.</para>
/// </summary>
public sealed class ChonNhaCungCapAnhTests
{
    private static GeminiSettings Gemini(string khoa = "khoa-123", string model = "gemini-3.1-flash-image")
        => new() { ApiKey = khoa, ImageModel = model };

    private static PosterWorkerSettings MayTram(bool bat = true, string khoa = "khoa-may-tram")
        => new() { Enabled = bat, ApiKey = khoa };

    private static CloudflareSettings Cloudflare(string acc = "acc", string token = "tok")
        => new() { AccountId = acc, ApiToken = token };

    private static NhaCungCapAnh Chon(GeminiSettings g, PosterWorkerSettings m, CloudflareSettings c)
        => AiImageProvider.Chon(g, m, c);

    [Fact]
    public void CauHinh_DU_CA_BA_ThiChonGemini()
        => Chon(Gemini(), MayTram(), Cloudflare()).Should().Be(NhaCungCapAnh.Gemini);

    [Fact]
    public void KhongKhaiGemini_ThiChonHangDoiMayTram()
        => Chon(Gemini(model: ""), MayTram(), Cloudflare()).Should().Be(NhaCungCapAnh.HangDoiMayTram);

    [Fact]
    public void KhongGeminiKhongMayTram_ThiChonCloudflare()
        => Chon(Gemini(model: ""), MayTram(bat: false), Cloudflare()).Should().Be(NhaCungCapAnh.Cloudflare);

    [Fact]
    public void KhongCauHinh_Gi_ThiRoiVeOpenAi()
        => Chon(Gemini(model: ""), MayTram(bat: false), Cloudflare(acc: "", token: ""))
            .Should().Be(NhaCungCapAnh.OpenAi);

    [Fact]
    public void ChiCoKhoaMaKhongKhaiModelAnh_THI_KHONG_DungGemini()
    {
        // Ca quan trọng nhất. Khoá Gemini dùng CHUNG với kiểm duyệt nội dung, và kiểm duyệt chạy được trên bậc miễn
        // phí còn sinh ảnh thì KHÔNG. Nếu "có khoá là bật sinh ảnh" thì mọi môi trường chỉ cấu hình kiểm duyệt sẽ
        // lặng lẽ chuyển sang một nhà cung cấp luôn thất bại.
        AiImageProvider.UseGemini(Gemini(model: "")).Should().BeFalse();
        Chon(Gemini(model: ""), MayTram(bat: false), Cloudflare()).Should().NotBe(NhaCungCapAnh.Gemini);
    }

    [Fact]
    public void ChiKhaiModelAnhMaKhongCoKhoa_THI_KHONG_DungGemini()
        => AiImageProvider.UseGemini(Gemini(khoa: "")).Should().BeFalse();

    [Fact]
    public void KhoaChiCoKhoangTrang_KhongTinhLaCoKhoa()
        => AiImageProvider.UseGemini(Gemini(khoa: "   ")).Should().BeFalse();

    [Fact]
    public void BatMayTramMaQuenKhoa_ThiKHONG_VaoHangDoi()
    {
        // Để hệ thống nhận đơn vào một hàng đợi không bao giờ có người lấy là tệ hơn việc quay về nhà cung cấp gọi thẳng.
        Chon(Gemini(model: ""), MayTram(khoa: ""), Cloudflare()).Should().Be(NhaCungCapAnh.Cloudflare);
    }
}
