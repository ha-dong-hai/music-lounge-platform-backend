using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Uploads;

/// <summary>
/// MLACP-416. Ảnh người dùng (ảnh công khai và ảnh CCCD) đang được ghi vào chính thư mục được deploy, nên Azure phải
/// ghi đè file ngay trên thư mục app đang chạy — mỗi lần deploy là ~2 phút lỗi 500 (<c>BadImageFormatException</c>
/// trong log 15/09), và cũng vì vậy không bao giờ deploy được kèm <c>--clean true</c>. Sau task này nơi lưu file cấu
/// hình được, để trên Azure trỏ ra vùng lưu trữ bền nằm ngoài thư mục deploy.
/// </summary>
[Collection("Integration")]
public sealed class StorageRootPathTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ml-storage-{Guid.NewGuid():N}");
    private readonly List<string> _thuMucTam = [];

    public StorageRootPathTests(ApiFactory factory) => _factory = factory;

    public void Dispose()
    {
        foreach (var d in _thuMucTam.Append(_root))
            try { if (Directory.Exists(d)) Directory.Delete(d, recursive: true); } catch { /* dọn dẹp thôi */ }
    }

    [Fact]
    public async Task CoCauHinh_KhongTaoThuMucTrongThuMucDeploy()
    {
        // MLACP-417: khi chay tu goi (WEBSITE_RUN_FROM_PACKAGE), thu muc deploy la CHI DOC — mot lenh tao thu muc o do
        // lam app chet ngay luc khoi dong. Dung mot thu muc goc TAM cho host nay (khong dung thu muc du an that, noi
        // dang chua anh dev) roi xem khoi dong co dong vao no khong.
        var thuMucDeployGia = Path.Combine(Path.GetTempPath(), $"ml-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thuMucDeployGia);
        _thuMucTam.Add(thuMucDeployGia);
        var duAn = _factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        foreach (var cauHinh in Directory.GetFiles(duAn, "appsettings*.json").Where(f => !f.Contains(".Local.")))
            File.Copy(cauHinh, Path.Combine(thuMucDeployGia, Path.GetFileName(cauHinh)));

        var factory = _factory.WithWebHostBuilder(b =>
        {
            b.UseContentRoot(thuMucDeployGia);
            b.UseSetting("Storage:RootPath", _root);
        });
        var res = await factory.CreateClient().GetAsync("/health");

        res.EnsureSuccessStatusCode();
        Directory.Exists(Path.Combine(thuMucDeployGia, "wwwroot", "uploads")).Should().BeFalse(
            "khoi dong khong duoc tao thu muc trong thu muc deploy khi da co noi luu rieng — cho do se la chi doc");
        Directory.Exists(Path.Combine(_root, "wwwroot", "uploads")).Should().BeTrue("thu muc moi phai duoc tao");
    }

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact]
    public async Task CoCauHinh_FileDuocLuuNgoaiThuMucDeploy()
    {
        var storage = new LocalFileStorageService(Options.Create(new StorageSettings { RootPath = _root }));
        using var png = new MemoryStream(Png());

        var url = await storage.SaveImageAsync(png, "anh-quan-cafe.png");

        url.Should().StartWith("/uploads/", "đường dẫn công khai phải giữ nguyên để dữ liệu cũ và frontend không gãy");
        var savedFile = Path.Combine(_root, "wwwroot", "uploads", Path.GetFileName(url));
        File.Exists(savedFile).Should().BeTrue($"file phải nằm trong thư mục được cấu hình: {savedFile}");
        Path.GetFullPath(savedFile).Should().NotStartWith(
            Path.GetFullPath(Directory.GetCurrentDirectory()),
            "đây chính là mục đích: file người dùng không còn nằm trong thư mục được deploy");
    }

    [Fact]
    public async Task KhongCauHinh_GiuNguyenHanhViCu()
    {
        var storage = new LocalFileStorageService(Options.Create(new StorageSettings()));
        using var png = new MemoryStream(Png());

        var url = await storage.SaveImageAsync(png, "anh-san-khau.png");

        var savedFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", Path.GetFileName(url));
        File.Exists(savedFile).Should().BeTrue("môi trường dev không được đổi hành vi");
        File.Delete(savedFile);
    }

    [Fact]
    public async Task AnhLuuNgoaiThuMucDeploy_VanTaiVeDuocQuaDuongDanCu()
    {
        // KHONG dispose factory phu nay: Program.cs goi Log.CloseAndFlush() khi host tat, nen huy no giua phien se dong
        // luon Serilog toan cuc va cac test doc nhat ky (RequestLoggingLevelTests) sau do khong con thay gi.
        var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Storage:RootPath", _root));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderUserId, SeedHelper.AudienceId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.HeaderRole, "Audience");
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "anh-phong-tra.png");

        var upload = await client.PostAsync("/api/v1/uploads/images", form);
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var url = (await upload.Content.ReadAsStringAsync()).Split("\"url\":\"")[1].Split('"')[0];

        var download = await client.GetAsync(url);

        download.StatusCode.Should().Be(HttpStatusCode.OK, "ảnh phải phục vụ được từ nơi lưu mới");
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(Png());
    }
}
