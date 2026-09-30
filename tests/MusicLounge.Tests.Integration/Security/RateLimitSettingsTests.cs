using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>
/// Ngưỡng giới hạn tần suất đọc từ appsettings (mục RateLimiting) thay vì viết cứng trong Program.cs.
///
/// Bộ giới hạn bị TẮT ở môi trường Testing (Program.cs), nên các bài này KHÔNG chứng minh middleware thật sự chặn ở
/// đúng ngưỡng — phần đó phải kiểm bằng tay trên một host Development. Các bài này chứng minh ba điều: mặc định không
/// đổi, cấu hình ghi đè được, và Program.cs thật sự dùng giá trị cấu hình chứ không còn con số viết cứng.
/// </summary>
public sealed class RateLimitSettingsTests
{
    private static IConfiguration CauHinh(params (string Key, string? Value)[] cap) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(cap.Select(c => new KeyValuePair<string, string?>(c.Key, c.Value)))
            .Build();

    [Fact]
    public void KhongKhaiGi_GiuDungNguongCu_100Va10()
    {
        var s = RateLimitSettings.From(CauHinh());

        s.GlobalPermitPerMinute.Should().Be(100);
        s.AuthPermitPerMinute.Should().Be(10);
    }

    [Fact]
    public void KhaiTrongCauHinh_ThiDungGiaTriDo()
    {
        var s = RateLimitSettings.From(CauHinh(
            ("RateLimiting:GlobalPermitPerMinute", "1200"),
            ("RateLimiting:AuthPermitPerMinute", "120")));

        s.GlobalPermitPerMinute.Should().Be(1200);
        s.AuthPermitPerMinute.Should().Be(120);
    }

    [Fact]
    public void ChiKhaiMotNguong_NguongConLaiGiuMacDinh()
    {
        var s = RateLimitSettings.From(CauHinh(("RateLimiting:AuthPermitPerMinute", "60")));

        s.GlobalPermitPerMinute.Should().Be(RateLimitSettings.DefaultGlobalPermitPerMinute);
        s.AuthPermitPerMinute.Should().Be(60);
    }

    [Theory]
    [InlineData("RateLimiting:GlobalPermitPerMinute", "0")]
    [InlineData("RateLimiting:GlobalPermitPerMinute", "-5")]
    [InlineData("RateLimiting:AuthPermitPerMinute", "0")]
    [InlineData("RateLimiting:AuthPermitPerMinute", "-1")]
    public void GiaTriTuKhongTroXuong_DungLucKhoiDong_KhongLangLeDungMacDinh(string khoa, string giaTri)
    {
        var goi = () => RateLimitSettings.From(CauHinh((khoa, giaTri)));

        goi.Should().Throw<InvalidOperationException>().WithMessage("*phải lớn hơn 0*");
    }

    [Fact]
    public void AppsettingsGocVaMacDinhTrongCode_LaCungMotCapSo()
    {
        // appsettings.json ghi sẵn hai ngưỡng cho dễ thấy; nếu lệch với hằng trong code thì "không khai gì" và "bản
        // gốc" cho ra hai hành vi khác nhau mà không ai biết.
        var duongDan = Path.Combine(ThuMucGoc(), "src", "MusicLounge.Api", "appsettings.json");
        var s = RateLimitSettings.From(new ConfigurationBuilder().AddJsonFile(duongDan).Build());

        s.GlobalPermitPerMinute.Should().Be(RateLimitSettings.DefaultGlobalPermitPerMinute);
        s.AuthPermitPerMinute.Should().Be(RateLimitSettings.DefaultAuthPermitPerMinute);
    }

    [Fact]
    public void ProgramCs_DungGiaTriCauHinh_KhongConSoVietCung()
    {
        var ma = File.ReadAllText(Path.Combine(ThuMucGoc(), "src", "MusicLounge.Api", "Program.cs"));

        // Chặn "quét trúng số không": phải thấy đúng hai chỗ gán PermitLimit thì bài quét mới có nghĩa.
        var cacChoGan = Regex.Matches(ma, @"PermitLimit\s*=\s*([^,\r\n]+)");
        cacChoGan.Count.Should().Be(2, "Program.cs có đúng hai bộ giới hạn: chung và nhóm auth");

        cacChoGan.Select(m => m.Groups[1].Value.Trim()).Should().BeEquivalentTo(
            ["rateLimit.GlobalPermitPerMinute", "rateLimit.AuthPermitPerMinute"]);
    }

    private static string ThuMucGoc()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);
        while (thuMuc is not null && !File.Exists(Path.Combine(thuMuc.FullName, "MusicLounge.sln")))
            thuMuc = thuMuc.Parent;
        return thuMuc?.FullName ?? throw new InvalidOperationException("Không tìm thấy MusicLounge.sln từ thư mục test.");
    }
}
