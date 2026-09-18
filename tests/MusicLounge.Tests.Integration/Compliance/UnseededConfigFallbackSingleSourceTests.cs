using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-446. Một khoá <c>system_config</c> KHÔNG được seed thì giá trị mặc định viết trong code chính là
/// chính sách đang chạy thật — không có dòng nào trong database để ghi đè. Nếu khoá đó được đọc ở nhiều
/// nơi, mỗi nơi phải lấy mặc định từ cùng một hằng số; nếu không, ai sửa một chỗ quên chỗ kia thì hai
/// đường code áp hai chính sách khác nhau mà không test nào đỏ.
///
/// Trước task này có bốn khoá như vậy, đều kèm chú thích dặn "phải giống nhau" mà không gì bắt buộc:
/// <list type="bullet">
/// <item><c>refund_sla_hours</c> — 4 nơi: hai chỗ ghi thẳng 72, hai hằng <c>DefaultSlaHours</c> của hai lớp job.</item>
/// <item><c>content_report_sla_hours</c> — 2 nơi ghi thẳng 48.</item>
/// <item><c>walkin_commission_enabled</c> — 2 nơi ghi thẳng <c>false</c>; lệch nhau là chi tiền hai lần cho một vé.</item>
/// <item><c>show_auto_end_grace_hours</c> — 3 job, mỗi job một hằng private <c>DefaultGraceHours = 6</c>.</item>
/// </list>
///
/// Quy tắc: khoá không seed mà đọc ở từ hai nơi trở lên thì mọi nơi phải dùng CÙNG MỘT tham chiếu có tên
/// lớp (<c>Lop.HangSo</c>). So chữ thôi là không đủ — ba job cùng viết <c>DefaultGraceHours</c> nhưng đó là
/// ba hằng khác nhau, đúng cái lỗi vừa sửa. Cách dễ nhất để đạt là đọc qua một hàm dùng chung
/// (<c>RefundSla.SlaHoursAsync</c>…), khi đó khoá chỉ còn một nơi đọc.
///
/// Khoá ĐÃ seed không nằm trong phạm vi: dòng trong database luôn thắng, và không có đường nào xoá một
/// dòng cấu hình ngoài migration (PUT chỉ sửa giá trị), nên mặc định của chúng không bao giờ được áp.
/// </summary>
[Collection("Integration")]
public sealed class UnseededConfigFallbackSingleSourceTests
{
    private readonly ApiFactory _factory;

    public UnseededConfigFallbackSingleSourceTests(ApiFactory factory) => _factory = factory;

    private static readonly Regex ThamChieuCoTenLop =
        new(@"^[A-Za-z_]\w*(\.[A-Za-z_]\w*)+$", RegexOptions.Compiled);

    [Fact]
    public void KhoaKhongSeed_DocONhieuNoi_PhaiLayMacDinhTuCungMotHangSo()
    {
        var seeded = KhoaDuocSeed();
        seeded.Should().NotBeEmpty("bản thân phép quét phải không được im lặng khớp số không");

        var diemDoc = DiemDocCauHinh(out var soFile);
        soFile.Should().BeGreaterThan(100,
            "quét trúng quá ít file nghĩa là sai đường dẫn — test sẽ xanh vì không thấy gì chứ không phải vì đúng");
        diemDoc.Should().NotBeEmpty("bản thân phép quét phải không được im lặng khớp số không");

        var vi_pham = diemDoc
            .Where(d => !seeded.Contains(d.Khoa))
            .GroupBy(d => d.Khoa)
            .Where(g => g.Count() > 1)
            .Where(g => g.Select(d => d.MacDinh).Distinct().Count() > 1
                        || g.Any(d => !ThamChieuCoTenLop.IsMatch(d.MacDinh)))
            .Select(g => $"{g.Key}: " + string.Join("; ", g.Select(d => $"{d.MacDinh} @ {d.ViTri}")))
            .ToList();

        // Gộp thành một chuỗi: BeEmpty trên danh sách chỉ in phần tử đầu tiên, còn người sửa cần thấy hết.
        string.Join(Environment.NewLine, vi_pham).Should().BeEmpty(
            "khoá không seed thì mặc định trong code là chính sách đang chạy; đọc ở nhiều nơi mà mặc định không " +
            "cùng một hằng số (có tên lớp) thì sửa một chỗ là hai đường code áp hai chính sách. Gom về một hàm " +
            "đọc dùng chung như RefundSla.SlaHoursAsync");
    }

    private IReadOnlySet<string> KhoaDuocSeed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.SystemConfigs.AsNoTracking().Select(c => c.ConfigKey).ToHashSet(StringComparer.Ordinal);
    }

    private sealed record DiemDoc(string Khoa, string MacDinh, string ViTri);

    /// <summary>
    /// Quét mã nguồn tìm mọi <c>Get*Async(khoá, mặc định, …)</c>, tách đối số theo độ sâu ngoặc để lời gọi xuống
    /// dòng vẫn đọc đúng. Phải đọc mã nguồn: hằng số được nội tuyến lúc biên dịch nên assembly không còn phân biệt
    /// được "cùng một hằng" với "hai hằng cùng giá trị".
    /// </summary>
    private static List<DiemDoc> DiemDocCauHinh(out int soFile)
    {
        var hangSo = typeof(ConfigKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

        var goi = new Regex(@"Get(?:Int|Decimal|Bool|String)Async\s*\(", RegexOptions.Compiled);
        var ket = new List<DiemDoc>();
        soFile = 0;

        var src = ThuMucSrc();
        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var duong = file.Replace('\\', '/');
            if (duong.Contains("/obj/") || duong.Contains("/bin/") || duong.Contains("/Migrations/")) continue;
            soFile++;

            var text = File.ReadAllText(file);
            foreach (Match m in goi.Matches(text))
            {
                var doiSo = TachDoiSo(text, m.Index + m.Length);
                if (doiSo.Count < 2) continue; // khai báo trong interface/triển khai, không phải lời gọi

                var khoa = doiSo[0];
                string? giaTri = khoa.StartsWith("ConfigKeys.", StringComparison.Ordinal)
                    ? hangSo.GetValueOrDefault(khoa["ConfigKeys.".Length..])
                    : khoa.Length > 1 && khoa[0] == '"' && khoa[^1] == '"' ? khoa[1..^1] : null;
                if (giaTri is null) continue; // tham số "string key" của chính ISystemConfigService

                var dong = text.AsSpan(0, m.Index).Count('\n') + 1;
                ket.Add(new DiemDoc(giaTri, doiSo[1], $"{Path.GetRelativePath(src, file)}:{dong}"));
            }
        }

        return ket;
    }

    private static List<string> TachDoiSo(string text, int batDau)
    {
        var doiSo = new List<string>();
        var sau = 1;
        var hienTai = new System.Text.StringBuilder();
        for (var i = batDau; i < text.Length && sau > 0; i++)
        {
            var c = text[i];
            if (c is '(' or '[' or '{') sau++;
            else if (c is ')' or ']' or '}')
            {
                sau--;
                if (sau == 0) break;
            }

            if (c == ',' && sau == 1)
            {
                doiSo.Add(Regex.Replace(hienTai.ToString(), @"\s+", " ").Trim());
                hienTai.Clear();
            }
            else hienTai.Append(c);
        }

        doiSo.Add(Regex.Replace(hienTai.ToString(), @"\s+", " ").Trim());
        return doiSo;
    }

    private static string ThuMucSrc()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);
        while (thuMuc is not null && !File.Exists(Path.Combine(thuMuc.FullName, "MusicLounge.sln")))
            thuMuc = thuMuc.Parent;

        thuMuc.Should().NotBeNull("không tìm thấy gốc repo (MusicLounge.sln) từ thư mục chạy test");
        var src = Path.Combine(thuMuc!.FullName, "src");
        Directory.Exists(src).Should().BeTrue("không tìm thấy thư mục src/ ở gốc repo");
        return src;
    }
}
