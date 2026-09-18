using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-447. Thông báo 404 đi thẳng vào body cho người dùng đọc, mà trước đây nó là
/// <c>'MusicLoungeEntity' với key (5) không tồn tại.</c> — tên lớp C# lẫn trong câu tiếng Việt, ở 269 chỗ gọi.
/// Sửa một lần trong <see cref="NotFoundException"/>; các test dưới đây giữ cho nó không quay lại.
/// </summary>
[Collection("Integration")]
public sealed class NotFoundMessageTests
{
    private const string NhanChung = "dữ liệu yêu cầu";
    private readonly ApiFactory _factory;

    public NotFoundMessageTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void TenKyThuat_DoiSangNhanTiengViet()
    {
        new NotFoundException("MusicLoungeEntity", 5).Message.Should().Be("Không tìm thấy phòng trà (mã 5).");
        new NotFoundException("LoungeShow", 12).Message.Should().Be("Không tìm thấy buổi hòa nhạc (mã 12).");
        new NotFoundException("EventModeration for Show", 3).Message
            .Should().Be("Không tìm thấy yêu cầu kiểm duyệt buổi hòa nhạc (mã 3).",
                "có dấu cách không có nghĩa là câu cho người đọc — đây vẫn là tên kỹ thuật");
    }

    [Fact]
    public void TenKyThuatChuaCoNhan_DungCauChung_KhongBaoGioLoTenLop()
    {
        var message = new NotFoundException("SomeFutureEntity", 7).Message;

        message.Should().Be($"Không tìm thấy {NhanChung} (mã 7).");
        message.Should().NotContain("SomeFutureEntity");
    }

    [Fact]
    public void ChuoiDaLaTiengViet_GiuNguyen()
        => new NotFoundException("Người nhận (theo email)", "a@b.vn").Message
            .Should().Be("Không tìm thấy người nhận (theo email) (mã a@b.vn).");

    [Fact]
    public void TenKyThuatVanGiuLaiChoLog()
        => new NotFoundException("MusicLoungeEntity", 5).ResourceName.Should().Be("MusicLoungeEntity");

    /// <summary>
    /// Mọi tên đang truyền vào <see cref="NotFoundException"/> trong src/ phải có nhãn riêng. Tên thiếu nhãn không làm
    /// lộ tên lớp nữa (đã có câu chung), nhưng "Không tìm thấy dữ liệu yêu cầu" thì không nói cho người dùng biết cái gì
    /// không tìm thấy — nên thêm entity mới là phải thêm nhãn. Cùng cách với các test quét khác của dự án.
    /// </summary>
    [Fact]
    public void MoiTenDangDuocTruyenVao_DeuCoNhanRieng()
    {
        var goi = new Regex(@"NotFoundException\(\s*(?:nameof\((?<ten>\w+)\)|""(?<chuoi>[^""]+)"")", RegexOptions.Compiled);
        var ten = new HashSet<string>(StringComparer.Ordinal);
        var soFile = 0;

        foreach (var file in Directory.EnumerateFiles(ThuMucSrc(), "*.cs", SearchOption.AllDirectories))
        {
            var duong = file.Replace('\\', '/');
            if (duong.Contains("/obj/") || duong.Contains("/bin/")) continue;
            soFile++;
            foreach (Match m in goi.Matches(File.ReadAllText(file)))
                ten.Add(m.Groups["ten"].Success ? m.Groups["ten"].Value : m.Groups["chuoi"].Value);
        }

        // Một chỗ gọi truyền tên sinh động: new NotFoundException(targetType.ToString(), …) với ReportTargetType.
        foreach (var t in Enum.GetNames<ReportTargetType>()) ten.Add(t);

        soFile.Should().BeGreaterThan(100, "quét trúng quá ít file nghĩa là sai đường dẫn — test sẽ xanh vì không thấy gì");
        ten.Should().HaveCountGreaterThan(30, "bản thân phép quét phải không được im lặng khớp số không");

        var thieuNhan = ten
            .Where(t => new NotFoundException(t, 1).Message.Contains(NhanChung))
            .OrderBy(t => t)
            .ToList();

        string.Join(", ", thieuNhan).Should().BeEmpty(
            "mỗi tên truyền vào NotFoundException cần một nhãn tiếng Việt trong NotFoundException.Nhan — thiếu thì người " +
            "dùng chỉ đọc được 'Không tìm thấy dữ liệu yêu cầu'");
    }

    /// <summary>Đi hết đường thật: middleware bắt ngoại lệ và đưa câu thông báo vào body 404.</summary>
    [Fact]
    public async Task Api404_TraThongBaoTiengViet_KhongLoTenLop()
    {
        var res = await _factory.CreateClient().GetAsync("/api/v1/lounges/999999");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var message = body.RootElement.GetProperty("message").GetString();

        message.Should().Be("Không tìm thấy phòng trà (mã 999999).");
        message.Should().NotMatchRegex(@"'[A-Z][A-Za-z]+'", "không được còn tên lớp trong dấu nháy như câu cũ");
    }

    private static string ThuMucSrc()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);
        while (thuMuc is not null && !File.Exists(Path.Combine(thuMuc.FullName, "MusicLounge.sln")))
            thuMuc = thuMuc.Parent;

        thuMuc.Should().NotBeNull("không tìm thấy gốc repo (MusicLounge.sln) từ thư mục chạy test");
        return Path.Combine(thuMuc!.FullName, "src");
    }
}
