using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using MusicLounge.Application.Notifications;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-455. <c>referenceType</c> là thứ frontend dùng để mở đúng màn hình khi bấm vào thông báo. Trước task này có 21
/// giá trị với ba kiểu viết lẫn lộn (<c>venue_penalty</c>, <c>fnbOrder</c>, <c>kyc-review</c>), và <c>refund</c> lẫn
/// <c>refund_request</c> cùng trỏ một thứ. Test giữ cho từ vựng không trôi lại.
/// </summary>
public sealed class NotificationReferenceTypeVocabularyTests
{
    private static IReadOnlyList<string> TuVung() => typeof(NotificationReferenceTypes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    [Fact]
    public void MoiGiaTriTrongTuVung_DeuLaSnakeCase_VaKhongTrungNhau()
    {
        var tuVung = TuVung();

        tuVung.Should().HaveCountGreaterThan(15, "bản thân phép đọc phải không được im lặng khớp số không");
        tuVung.Should().OnlyContain(v => Regex.IsMatch(v, "^[a-z]+(_[a-z]+)*$"),
            "chỉ snake_case — không camelCase như fnbOrder, không gạch ngang như kyc-review");
        tuVung.Should().OnlyHaveUniqueItems("hai tên cho cùng một tài nguyên chính là lỗi cần sửa");
    }

    /// <summary>
    /// Mọi chuỗi truyền vào tham số <c>referenceType:</c> trong src/ phải nằm trong từ vựng. Chỗ gọi vẫn dùng chuỗi cho
    /// đọc liền mạch cùng nội dung thông báo ngay tại đó, nên ràng buộc nằm ở đây chứ không phải ở trình biên dịch.
    /// </summary>
    [Fact]
    public void MoiChuoiReferenceTypeTrongCode_DeuThuocTuVung()
    {
        var tuVung = TuVung().ToHashSet(StringComparer.Ordinal);
        var goi = new Regex("referenceType: *\"([^\"]+)\"", RegexOptions.Compiled);
        var thay = new Dictionary<string, string>(StringComparer.Ordinal);
        var soFile = 0;

        foreach (var file in Directory.EnumerateFiles(ThuMucSrc(), "*.cs", SearchOption.AllDirectories))
        {
            var duong = file.Replace('\\', '/');
            if (duong.Contains("/obj/") || duong.Contains("/bin/")) continue;
            soFile++;
            foreach (Match m in goi.Matches(File.ReadAllText(file)))
                thay[m.Groups[1].Value] = Path.GetFileName(file);
        }

        soFile.Should().BeGreaterThan(100, "quét trúng quá ít file nghĩa là sai đường dẫn — test sẽ xanh vì không thấy gì");
        thay.Should().HaveCountGreaterThan(10, "bản thân phép quét phải không được im lặng khớp số không");

        var ngoaiTuVung = thay
            .Where(kv => !tuVung.Contains(kv.Key))
            .Select(kv => $"{kv.Key} ({kv.Value})")
            .OrderBy(s => s)
            .ToList();

        string.Join(", ", ngoaiTuVung).Should().BeEmpty(
            "giá trị referenceType mới phải được khai trong NotificationReferenceTypes — đó là hợp đồng frontend dựa vào " +
            "để mở đúng màn hình, và cũng là nơi ghi ReferenceId khi đó là mã của cái gì");
    }

    /// <summary>
    /// Các truy vấn chống gửi trùng so <c>ReferenceType == "..."</c> phải dùng cùng từ vựng — đổi tên ở chỗ phát mà quên
    /// truy vấn thì thông báo bị gửi lại mỗi lần job chạy, đúng loại tiếng ồn MLACP-348 đã dọn.
    /// </summary>
    [Fact]
    public void MoiChuoiSoSanhReferenceTypeTrongTruyVan_DeuThuocTuVung()
    {
        var tuVung = TuVung().ToHashSet(StringComparer.Ordinal);
        // Chỉ bắt n.ReferenceType của thông báo. Payment.ReferenceType (p.ReferenceType) là từ vựng khác — TicketHold,
        // WalkIn, Donation, Subscription, FnbOrder — cố ý không gộp.
        var soSanh = new Regex(@"\bn\.ReferenceType == *""([^""]+)""", RegexOptions.Compiled);
        var thay = new List<(string GiaTri, string File)>();

        foreach (var file in Directory.EnumerateFiles(ThuMucSrc(), "*.cs", SearchOption.AllDirectories))
        {
            var duong = file.Replace('\\', '/');
            if (duong.Contains("/obj/") || duong.Contains("/bin/")) continue;
            foreach (Match m in soSanh.Matches(File.ReadAllText(file)))
                thay.Add((m.Groups[1].Value, Path.GetFileName(file)));
        }

        thay.Should().HaveCountGreaterThan(5, "bản thân phép quét phải không được im lặng khớp số không");
        thay.Where(x => !tuVung.Contains(x.GiaTri))
            .Select(x => $"{x.GiaTri} ({x.File})")
            .Should().BeEmpty("truy vấn chống gửi trùng đang so với một tên không còn được phát nữa");
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
