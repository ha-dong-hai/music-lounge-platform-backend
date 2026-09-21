using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Configuration;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;
using Audit = MusicLounge.Infrastructure.Configuration.ConfigurationAudit;

namespace MusicLounge.Tests.Integration.Observability;

/// <summary>
/// MLACP-420. Thiếu cấu hình từng làm tính năng chết âm thầm nhiều lần: thiếu <c>Firebase:ProjectId</c> khiến đăng nhập
/// Google hỏng với mọi người dùng suốt nhiều tuần (phát hiện 15/09), thiếu <c>Mux:WebhookSecret</c> làm livestream mất
/// các chuyển trạng thái tự động (16/09). Bảng kiểm này biến "im lặng" thành danh sách đọc được.
/// </summary>
[Collection("Integration")]
public sealed class ConfigurationAuditTests
{
    private readonly ApiFactory _factory;

    public ConfigurationAuditTests(ApiFactory factory) => _factory = factory;

    private sealed class MoiTruong(string ten) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = ten;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static Audit TaoBangKiem(
        string firebaseProjectId = "sign-in-52d07", string muxWebhookSecret = "whsec", string geminiKey = "gem",
        string openAiKey = "sk", string cloudflareAccount = "", string cloudflareToken = "",
        string firebaseCredentials = "/duong/dan.json", string performerUrl = "https://x/y",
        string processingUrl = "https://x/dang-xu-ly", string successUrl = "https://x/thanh-cong",
        string moiTruong = "Production", string smsAccountSid = "AC123", string emailHost = "smtp.test",
        string stitcherUrl = "https://ghep-anh.test", string stitcherKey = "khoa-ghep-anh",
        string stitcherPublicUrl = "https://musiclounge-api.azurewebsites.net", string storageBucket = "",
        bool posterWorkerEnabled = false, string posterWorkerKey = "")
        => new(
            Options.Create(new FirebaseSettings
            {
                ProjectId = firebaseProjectId, CredentialsPath = firebaseCredentials, StorageBucket = storageBucket
            }),
            Options.Create(new MuxSettings { WebhookSecret = muxWebhookSecret }),
            Options.Create(new LivestreamSettings { Provider = "mux" }),
            Options.Create(new GeminiSettings { ApiKey = geminiKey }),
            Options.Create(new OpenAiSettings { ApiKey = openAiKey }),
            Options.Create(new CloudflareSettings { AccountId = cloudflareAccount, ApiToken = cloudflareToken }),
            Options.Create(new BusinessSettings
            {
                PerformerConfirmationUrl = performerUrl,
                PaymentProcessingUrl = processingUrl,
                PaymentSuccessUrl = successUrl,
                PaymentFailedUrl = "https://x/that-bai",
                PasswordResetUrl = "https://x/dat-lai-mat-khau"
            }),
            Options.Create(new SmsSettings { AccountSid = smsAccountSid, AuthToken = "tok", FromNumber = "+15551234567" }),
            Options.Create(new EmailSettings { Host = emailHost }),
            Options.Create(new PanoramaStitcherSettings
            {
                BaseUrl = stitcherUrl, ApiKey = stitcherKey, PublicBaseUrl = stitcherPublicUrl
            }),
            Options.Create(new PosterWorkerSettings { Enabled = posterWorkerEnabled, ApiKey = posterWorkerKey }),
            new MoiTruong(moiTruong));

    [Fact]
    public void CauHinhDayDu_KhongBaoThieuGi()
        => TaoBangKiem().Inspect().Should().BeEmpty();

    /// <summary>
    /// MLACP-479: bảng kiểm trước đây KHÔNG soi chế độ hàng đợi poster (máy trạm chạy Google Flow) — nó chỉ soi
    /// đường gọi thẳng. Bật cờ mà quên khoá thì hệ thống lặng lẽ quay về nhà cung cấp gọi thẳng, còn máy trạm gọi
    /// <c>/poster-jobs/claim</c> nhận 401; nhìn từ ngoài giống hệt lỗi lập trình, và không có chỗ nào cho thấy hai
    /// việc đó liên quan tới nhau.
    /// </summary>
    [Fact]
    public void BatHangDoiPosterMaThieuKhoa_BaoHong()
    {
        var gaps = TaoBangKiem(posterWorkerEnabled: true, posterWorkerKey: "").Inspect();

        gaps.Should().ContainSingle(g => g.Key == "PosterWorker:ApiKey")
            .Which.Severity.Should().Be(ConfigurationGapSeverity.Broken);
        gaps.Single(g => g.Key == "PosterWorker:ApiKey").Impact.Should().Contain("401");
    }

    [Fact]
    public void BatHangDoiPosterDayDu_KhongDoiNhaCungCapGoiThang()
    {
        // Chạy hàng đợi thì không cần Cloudflare/OpenAI nữa. Báo thiếu ở đây là báo thừa, và báo thừa thì dạy người
        // vận hành bỏ qua cả bảng kiểm.
        var gaps = TaoBangKiem(posterWorkerEnabled: true, posterWorkerKey: "khoa-may-tram",
                               openAiKey: "", cloudflareAccount: "", cloudflareToken: "").Inspect();

        gaps.Should().NotContain(g => g.Key.StartsWith("Cloudflare:", StringComparison.Ordinal));
        gaps.Should().NotContain(g => g.Key == "PosterWorker:ApiKey");
    }

    [Fact]
    public void KhongBatHangDoiVaKhongCoNhaCungCapNao_VanBaoHong()
    {
        var gaps = TaoBangKiem(posterWorkerEnabled: false, openAiKey: "",
                               cloudflareAccount: "", cloudflareToken: "").Inspect();

        gaps.Should().Contain(g => g.Key.StartsWith("Cloudflare:", StringComparison.Ordinal)
                                   && g.Severity == ConfigurationGapSeverity.Broken);
    }

    [Fact]
    public void ThieuFirebaseProjectId_BaoDangNhapGoogleHong()
    {
        var gaps = TaoBangKiem(firebaseProjectId: "").Inspect();

        gaps.Should().ContainSingle(g => g.Key == "Firebase:ProjectId")
            .Which.Severity.Should().Be(ConfigurationGapSeverity.Broken);
        gaps.Single(g => g.Key == "Firebase:ProjectId").Impact.Should().Contain("đăng nhập bằng Google");
    }

    [Fact]
    public void ThieuCauHinhTwilio_BaoXacMinhSoDienThoaiHong()
    {
        // MLACP-426. Bảng kiểm trước đây không soi SMS, nên rà soát Azure 16/09 không phát hiện luồng xác minh số điện
        // thoại chưa bao giờ gửi được tin nào.
        var gap = TaoBangKiem(smsAccountSid: "").Inspect().Should().ContainSingle(g => g.Key.Contains("Sms:AccountSid")).Which;

        gap.Severity.Should().Be(ConfigurationGapSeverity.Broken);
        gap.Impact.Should().Contain("mã xác minh");
    }

    [Fact]
    public void ThieuMuxWebhookSecret_BaoMatChuyenTrangThaiTuDong()
    {
        // MLACP-428: câu cũ nói "không có bản xem lại" — nhưng livestream KHÔNG có tính năng xem lại (chủ xác nhận
        // 16/09). Webhook Mux chỉ lo chuyển trạng thái tự động, nên câu cảnh báo phải nói đúng thứ bị mất.
        var gap = TaoBangKiem(muxWebhookSecret: "").Inspect().Should().ContainSingle(g => g.Key == "Mux:WebhookSecret").Which;

        gap.Impact.Should().NotContain("xem lại").And.Contain("tự kết thúc");
    }

    [Fact]
    public void ThieuSmtp_BaoEmailQuanTrongKhongGui()
    {
        // MLACP-428. Thiếu SMTP thì email đặt lại mật khẩu, mã xác minh email và email mời nghệ sĩ tự xác nhận đều âm
        // thầm không gửi — trước đây bảng kiểm không soi mục này.
        var gap = TaoBangKiem(emailHost: "").Inspect().Should().ContainSingle(g => g.Key == "Email:Host").Which;

        gap.Severity.Should().Be(ConfigurationGapSeverity.Broken);
        gap.Impact.Should().Contain("đặt lại mật khẩu");
    }

    [Fact]
    public void ThieuDichVuGhepAnh360_BaoSuyGiam()
    {
        // MLACP-428. Đang thiếu thật trên Azure (rà soát 17/09): ghép ảnh báo lỗi nhưng không ai được cảnh báo. Là suy
        // giảm chứ chưa chết hẳn — chủ phòng trà vẫn tải được ảnh 360 dựng sẵn.
        var gap = TaoBangKiem(stitcherUrl: "").Inspect().Should().ContainSingle(g => g.Key == "PanoramaStitcher:BaseUrl").Which;

        gap.Severity.Should().Be(ConfigurationGapSeverity.Degraded);
        gap.Impact.Should().Contain("ảnh 360");
    }

    [Fact]
    public void CoDiaChiGhepAnhNhungThieuKhoa_VanBaoSuyGiam()
    {
        // MLACP-431: dịch vụ ghép ảnh từ chối mọi yêu cầu không kèm khoá. Chỉ soi BaseUrl thì bảng kiểm báo xanh trong khi
        // chủ phòng trà bấm ghép vẫn bị báo "tạm ngưng".
        TaoBangKiem(stitcherKey: "").Inspect().Should().ContainSingle(g => g.Key == "PanoramaStitcher:ApiKey")
            .Which.Severity.Should().Be(ConfigurationGapSeverity.Degraded);
    }

    [Fact]
    public void AnhLuuTrenDiaCucBo_ThieuPublicBaseUrl_BaoSuyGiam()
    {
        // Không có Firebase Storage thì ảnh lưu trên đĩa với đường dẫn tương đối — dịch vụ ghép ảnh cần URL đầy đủ để tải.
        TaoBangKiem(stitcherPublicUrl: "", storageBucket: "").Inspect()
            .Should().ContainSingle(g => g.Key == "PanoramaStitcher:PublicBaseUrl");
    }

    [Fact]
    public void AnhLuuTrenFirebase_KhongCanPublicBaseUrl()
        => TaoBangKiem(stitcherPublicUrl: "", storageBucket: "musiclounge.appspot.com").Inspect()
            .Should().NotContain(g => g.Key.Contains("PanoramaStitcher"));

    [Fact]
    public void ChuaCoGiCaChoGhepAnh_GopMotMucLietKeDuCacThieuSot()
        => TaoBangKiem(stitcherUrl: "", stitcherKey: "", stitcherPublicUrl: "").Inspect()
            .Should().ContainSingle(g => g.Feature == "Tour 360° phòng trà")
            .Which.Key.Should().Be("PanoramaStitcher:BaseUrl + PanoramaStitcher:ApiKey + PanoramaStitcher:PublicBaseUrl");

    [Fact]
    public void ThieuGeminiApiKey_NoiRoAnhKhongDuocKiemDuyet()
        => TaoBangKiem(geminiKey: "").Inspect()
            .Should().ContainSingle(g => g.Key == "Gemini:ApiKey" && g.Impact.Contains("KHÔNG được kiểm duyệt"));

    [Fact]
    public void CoCloudflare_ThiKhongCanOpenAi()
    {
        var gaps = TaoBangKiem(openAiKey: "", cloudflareAccount: "acc", cloudflareToken: "tok").Inspect();

        gaps.Should().NotContain(g => g.Feature == "Tạo poster AI");
    }

    [Fact]
    public void KhongCoNhaCungCapTaoAnhNao_BaoPosterHong()
        => TaoBangKiem(openAiKey: "", cloudflareAccount: "", cloudflareToken: "").Inspect()
            .Should().ContainSingle(g => g.Feature == "Tạo poster AI"
                                         && g.Severity == ConfigurationGapSeverity.Broken);

    [Fact]
    public void ChayThat_MaDuongDanVanTroVeLocalhost_ThiBaoDo()
        => TaoBangKiem(successUrl: "http://localhost:5173/payment/success").Inspect()
            .Should().Contain(g => g.Key == "Business:PaymentSuccessUrl"
                                   && g.Severity == ConfigurationGapSeverity.Broken);

    [Fact]
    public void MoiTruongDev_ThiKhongBao_localhost()
        => TaoBangKiem(successUrl: "http://localhost:5173/payment/success", moiTruong: "Development").Inspect()
            .Should().NotContain(g => g.Key == "Business:PaymentSuccessUrl");

    [Fact]
    public async Task ApiChoAdmin_TraDanhSachThieuCauHinh()
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin")
            .GetAsync("/api/v1/admin/configuration-audit");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Gemini:ApiKey", "môi trường test không cấu hình khoá Gemini nên phải bị nêu tên");
        body.Should().NotContain("ApiKey\":\"", "bảng kiểm không bao giờ được trả giá trị cài đặt");
    }

    [Fact]
    public async Task ApiChoAdmin_NguoiKhongPhaiAdmin_BiTuChoi()
    {
        var res = await _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner")
            .GetAsync("/api/v1/admin/configuration-audit");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
