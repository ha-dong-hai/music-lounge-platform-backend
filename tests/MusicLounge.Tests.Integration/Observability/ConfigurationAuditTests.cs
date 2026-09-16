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
/// Google hỏng với mọi người dùng suốt nhiều tuần (phát hiện 15/09), thiếu <c>Mux:WebhookSecret</c> làm mất bản xem lại
/// livestream (16/09). Bảng kiểm này biến "im lặng" thành danh sách đọc được.
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
        string moiTruong = "Production")
        => new(
            Options.Create(new FirebaseSettings { ProjectId = firebaseProjectId, CredentialsPath = firebaseCredentials }),
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
            new MoiTruong(moiTruong));

    [Fact]
    public void CauHinhDayDu_KhongBaoThieuGi()
        => TaoBangKiem().Inspect().Should().BeEmpty();

    [Fact]
    public void ThieuFirebaseProjectId_BaoDangNhapGoogleHong()
    {
        var gaps = TaoBangKiem(firebaseProjectId: "").Inspect();

        gaps.Should().ContainSingle(g => g.Key == "Firebase:ProjectId")
            .Which.Severity.Should().Be(ConfigurationGapSeverity.Broken);
        gaps.Single(g => g.Key == "Firebase:ProjectId").Impact.Should().Contain("đăng nhập bằng Google");
    }

    [Fact]
    public void ThieuMuxWebhookSecret_BaoMatBanXemLai()
        => TaoBangKiem(muxWebhookSecret: "").Inspect()
            .Should().ContainSingle(g => g.Key == "Mux:WebhookSecret" && g.Impact.Contains("xem lại"));

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
