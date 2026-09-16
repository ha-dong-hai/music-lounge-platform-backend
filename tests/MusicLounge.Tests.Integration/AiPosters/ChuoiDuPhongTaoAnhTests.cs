using FluentAssertions;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Exceptions;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-424. Trước đây hệ thống chọn cứng đúng một nhà cung cấp lúc khởi động: một lần gọi hỏng là tính năng tạo
/// poster chết, dù còn model khác hay nhà cung cấp khác dùng được. Đo thật ngày 16/09: khi tài khoản Cloudflare cạn
/// 10.000 neuron/ngày thì MỌI model đều bị từ chối (đã thử flux-1-schnell và stable-diffusion-xl-lightning, cùng một
/// thông báo 429) — nên đường lui phải bắc được sang nhà cung cấp khác, không chỉ sang model khác.
/// </summary>
public sealed class ChuoiDuPhongTaoAnhTests
{
    private sealed class MatXich(string ten, byte[]? ketQua, string? loi = null) : IAiImageGenerationService
    {
        public int SoLanGoi { get; private set; }

        public Task<byte[]> GenerateImageAsync(string prompt, CancellationToken ct = default)
        {
            SoLanGoi++;
            return loi is null
                ? Task.FromResult(ketQua!)
                : Task.FromException<byte[]>(new ExternalServiceException(ten, loi));
        }
    }

    private static readonly byte[] Anh = [1, 2, 3];

    [Fact]
    public async Task MatXichDauHong_TuChuyenSangMatXichSau()
    {
        var hong = new MatXich("Cloudflare", null, "429 TooManyRequests");
        var chay = new MatXich("OpenAI", Anh);

        var bytes = await new FallbackAiImageGenerationService([hong, chay]).GenerateImageAsync("poster");

        bytes.Should().Equal(Anh);
        hong.SoLanGoi.Should().Be(1);
        chay.SoLanGoi.Should().Be(1);
    }

    [Fact]
    public async Task MatXichDauChay_KhongGoiMatXichSau()
    {
        // Mat xich sau co the la nha cung cap TRA PHI — goi thua la mat tien that.
        var chay = new MatXich("Cloudflare", Anh);
        var duPhong = new MatXich("OpenAI", Anh);

        await new FallbackAiImageGenerationService([chay, duPhong]).GenerateImageAsync("poster");

        chay.SoLanGoi.Should().Be(1);
        duPhong.SoLanGoi.Should().Be(0);
    }

    [Fact]
    public async Task TatCaHongVìHetHanMuc_BaoTiengVietVaKhongLoNhaCungCap()
    {
        var a = new MatXich("Cloudflare", null,
            "429 TooManyRequests: {\"errors\":[{\"message\":\"you have used up your daily free allocation of 10,000 neurons, please upgrade to Cloudflare's Workers Paid plan\"}]}");
        var b = new MatXich("Cloudflare", null, "429 TooManyRequests: daily free allocation");

        var act = () => new FallbackAiImageGenerationService([a, b]).GenerateImageAsync("poster");

        var loi = await act.Should().ThrowAsync<ExternalServiceException>();
        loi.Which.Message.Should().Contain("hết lượt tạo ảnh AI miễn phí");
        // Khong duoc bao chu phong tra di nang cap goi cua NEN TANG, cung khong lo ten nha cung cap.
        loi.Which.Message.Should().NotContain("Cloudflare").And.NotContain("upgrade").And.NotContain("neurons");
    }

    [Fact]
    public async Task TatCaHongVìLyDoKhac_GiuNguyenLoiCuoiCung()
    {
        var a = new MatXich("Cloudflare", null, "500 InternalServerError");
        var b = new MatXich("OpenAI", null, "401 Unauthorized: sai khoá");

        var act = () => new FallbackAiImageGenerationService([a, b]).GenerateImageAsync("poster");

        (await act.Should().ThrowAsync<ExternalServiceException>()).Which.Message.Should().Contain("401");
    }

    [Fact]
    public async Task HuyYeuCau_KhongThuMatXichTiepTheo()
    {
        // Nguoi dung dong trinh duyet giua chung: khong duoc tiep tuc dot han muc cua cac mat xich con lai.
        var huy = new CancellationTokenSource();
        await huy.CancelAsync();
        var sau = new MatXich("OpenAI", Anh);

        var act = () => new FallbackAiImageGenerationService([new MatXich("Cloudflare", Anh), sau])
            .GenerateImageAsync("poster", huy.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        sau.SoLanGoi.Should().Be(0);
    }

    [Theory]
    // Chua cau hinh gi: giu nguyen hanh vi cu, bao loi cau hinh chu khong dung im lang.
    [InlineData("", "", new string[0], 0)]
    // Co Cloudflare, khong khai model: dung dung model mac dinh, 1 mat xich.
    [InlineData("acc", "tok", new string[0], 1)]
    // Khai 2 model theo thu tu uu tien: thanh 2 mat xich.
    [InlineData("acc", "tok", new[] { "@cf/black-forest-labs/flux-2-klein-4b", "@cf/black-forest-labs/flux-1-schnell" }, 2)]
    public void DanhSachModel_TaoDungSoMatXichCloudflare(
        string accountId, string token, string[] models, int mongDoi)
        => AiImageProvider.CloudflareModels(new CloudflareSettings
        {
            AccountId = accountId, ApiToken = token, ImageModels = models
        }).Should().HaveCount(mongDoi);

    [Fact]
    public void KhaiImageModelCu_VanDuocTonTrong()
    {
        // Azure dang dat Cloudflare__ImageModel; doi sang danh sach khong duoc lam hong cau hinh dang chay.
        AiImageProvider.CloudflareModels(new CloudflareSettings
        {
            AccountId = "acc", ApiToken = "tok", ImageModel = "@cf/black-forest-labs/flux-2-dev"
        }).Should().Equal("@cf/black-forest-labs/flux-2-dev");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("sk-that", true)]
    public void MatXichChuaCauHinh_BiLoaiKhoiChuoi(string apiKey, bool mongDoi)
        // Neu de mat xich chua cau hinh vao chuoi: Cloudflare het han muc (429) roi OpenAI nem "Chua cau hinh
        // OpenAI API key" — loi cau hinh do la loi CUOI CUNG nen se de mat thong bao "het luot trong ngay", chu
        // phong tra doc duoc dung mot cau khong lien quan gi toi ho.
        => AiImageProvider.UseOpenAi(new OpenAiSettings { ApiKey = apiKey }).Should().Be(mongDoi);
}
