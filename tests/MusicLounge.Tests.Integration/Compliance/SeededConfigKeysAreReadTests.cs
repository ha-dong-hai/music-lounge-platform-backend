using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Common.Interfaces;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-444. Một dòng <c>system_config</c> được seed nhưng không nơi nào đọc là một lời hứa suông:
/// nó hiện trên <c>GET /admin/system-config</c>, sửa được bằng <c>PUT</c>, ghi vào
/// <c>SystemConfigHistory</c> — và không đổi một hành vi nào. Admin không có cách nào biết.
///
/// Hệ thống này đã dính đúng bốn lần: <c>ai_auto_pass_threshold</c> và <c>ai_auto_reject_threshold</c>
/// (gỡ trước đó), rồi <c>ai_priority_high_threshold</c>, <c>ai_priority_low_threshold</c> và
/// <c>gateway_fee_rate</c> (gỡ ở MLACP-444), còn <c>appeal_auto_approve</c> thì nghiêm trọng hơn —
/// một công tắc an toàn tắt đi mà án phạt vẫn tiếp tục được gỡ tự động (MLACP-443).
///
/// Ba lần trước đều được sửa riêng lẻ kèm một ghi chú nhắc người sau nhớ; không lần nào ngăn được lần
/// kế tiếp. Test này thay lời nhắc đó bằng một điều kiện: thêm một khoá vào seed mà quên đọc nó thì
/// hỏng build. Cùng cách làm với <see cref="Helpers.BackgroundJobRegistrationTests"/>.
///
/// Danh sách khoá được đọc thẳng từ database mới dựng chứ không chép tay, nên không có chuyện test và
/// seed lệch nhau — và đó đúng là tập dòng Admin nhìn thấy ở <c>GET /admin/system-config</c>.
/// </summary>
[Collection("Integration")]
public sealed class SeededConfigKeysAreReadTests
{
    private readonly ApiFactory _factory;

    public SeededConfigKeysAreReadTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void MoiKhoaSystemConfigDuocSeed_PhaiCoItNhatMotChoDocTrongCode()
    {
        var seeded = KhoaDuocSeed();
        seeded.Should().NotBeEmpty("bản thân phép quét phải không được im lặng khớp số không");

        var doc = KhoaCoChoDoc(out var soFileDaQuet);
        soFileDaQuet.Should().BeGreaterThan(100,
            "phép quét phải thật sự đọc được mã nguồn — quét trúng số file quá ít nghĩa là sai đường dẫn, " +
            "và khi đó test sẽ xanh vì không tìm thấy gì chứ không phải vì mọi thứ đều đúng");
        doc.Should().NotBeEmpty("bản thân phép quét phải không được im lặng khớp số không");

        var chet = seeded.Except(doc).OrderBy(k => k).ToList();

        chet.Should().BeEmpty(
            "một khoá được seed mà không code nào đọc thì Admin sửa nó sẽ thấy 200 OK, thấy lịch sử " +
            "thay đổi được ghi lại, và không có gì thay đổi cả. Hoặc nối khoá vào chỗ dùng nó, hoặc " +
            "gỡ khỏi seed kèm migration xoá dòng — đừng để nó nằm đó trông như một chính sách đang " +
            "có hiệu lực");
    }

    private IReadOnlyCollection<string> KhoaDuocSeed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Đọc từ chính database chứ không chép tay danh sách: đây đúng là những dòng Admin nhìn thấy
        // ở GET /admin/system-config trên một database mới dựng từ seed.
        return db.SystemConfigs.AsNoTracking()
            .Select(c => c.ConfigKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Quét mã nguồn tìm mọi lời gọi <c>Get*Async(&lt;khoá&gt;, …)</c>. Phải đọc mã nguồn chứ không
    /// dùng reflection: hằng <c>const string</c> được nội tuyến lúc biên dịch, nên chỉ nhìn assembly
    /// thì không phân biệt được "khoá được đọc" với "khoá chỉ được khai báo hoặc liệt kê ở đâu đó".
    /// </summary>
    private static IReadOnlyCollection<string> KhoaCoChoDoc(out int soFileDaQuet)
    {
        var hangSo = typeof(ConfigKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

        var goiHam = new Regex(
            """Get(?:Int|Decimal|Bool|String)Async\s*\(\s*(?:ConfigKeys\.(?<ten>\w+)|"(?<chuoi>[a-z0-9_]+)")""",
            RegexOptions.Compiled);

        var ket = new HashSet<string>(StringComparer.Ordinal);
        var dem = 0;
        foreach (var file in Directory.EnumerateFiles(ThuMucSrc(), "*.cs", SearchOption.AllDirectories))
        {
            var duong = file.Replace('\\', '/');
            if (duong.Contains("/obj/", StringComparison.Ordinal)
                || duong.Contains("/bin/", StringComparison.Ordinal)
                || duong.Contains("/Migrations/", StringComparison.Ordinal))
                continue;

            dem++;
            foreach (Match m in goiHam.Matches(File.ReadAllText(file)))
            {
                var ten = m.Groups["ten"].Value;
                if (ten.Length > 0)
                {
                    if (hangSo.TryGetValue(ten, out var gia_tri)) ket.Add(gia_tri);
                }
                else
                {
                    ket.Add(m.Groups["chuoi"].Value);
                }
            }
        }

        soFileDaQuet = dem;
        return ket;
    }

    /// <summary>Đi ngược từ thư mục chạy test lên tới gốc repo (nơi có file .sln) rồi vào src/.</summary>
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
