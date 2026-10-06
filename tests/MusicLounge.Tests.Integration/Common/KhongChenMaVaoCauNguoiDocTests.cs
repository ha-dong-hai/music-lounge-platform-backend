using System.Text.RegularExpressions;
using FluentAssertions;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// MLACP-679. Câu backend tự soạn cho NGƯỜI ĐỌC (thông báo, lý do hoàn tiền, mô tả án phạt, câu lỗi) không được chèn mã
/// "#{x.Id}" — chủ dự án 06/10/2026: "các chỗ hiện tại show ID trên web rà soát để hiển thị thông tin". Gọi tên đối tượng
/// qua <c>TenDoiTuong</c> (án phạt theo loại + ngày, đơn đồ uống theo tiền + giờ, yêu cầu hoàn theo tiền + người mua +
/// buổi diễn, khiếu nại theo mã tra cứu) hoặc <c>ReferenceNames</c> ở các job.
///
/// <para><b>Được giữ mã</b> (không ai thấy trên web): mô tả bút toán sổ cái (<c>Description:</c>), OrderInfo gửi VNPay, và
/// mẫu câu log (<c>{PerformerId}</c> là tham số log có tên, không phải chèn mã vào chữ).</para>
/// </summary>
public sealed class KhongChenMaVaoCauNguoiDocTests
{
    // "#{" ngay trước một biểu thức kết thúc bằng Id/.Id — đúng dạng các câu cũ: "phạt #{penalty.Id}", "#{refundId}".
    private static readonly Regex ChenMa = new(@"#\{[A-Za-z_][\w.]*Id\}", RegexOptions.Compiled);

    [Fact]
    public void KhongCauNaoChenMaGuidVaoChu()
    {
        var tep = Directory.GetFiles(ThuMucSrc(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        tep.Should().HaveCountGreaterThan(300, "chặn quét trúng số không: phải thật sự đọc được mã nguồn");

        var vi = new List<string>();
        foreach (var f in tep)
        {
            var dong = File.ReadAllLines(f);
            for (var i = 0; i < dong.Length; i++)
            {
                var d = dong[i];
                if (!ChenMa.IsMatch(d)) continue;
                var t = d.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
                if (d.Contains("Description:") || d.Contains("OrderInfo:") || d.Contains("_logger.") || d.Contains("logger.Log")) continue;
                // Mẫu câu log viết xuống dòng: lời gọi Log nằm ở dòng ngay trên.
                if (i > 0 && dong[i - 1].Contains("logger.Log", StringComparison.OrdinalIgnoreCase)) continue;
                // Câu bút toán dựng sẵn vào biến rồi mới đưa vào LedgerLine (ProcessRefundRequest: gatewayOutflowNote).
                if (dong.Skip(Math.Max(0, i - 3)).Take(Math.Min(i, 3) + 1).Any(x => x.Contains("gatewayOutflowNote"))) continue;
                vi.Add($"{Path.GetFileName(f)}:{i + 1}: {t}");
            }
        }

        vi.Should().BeEmpty("câu cho người đọc phải gọi tên đối tượng, không chèn mã — xem TenDoiTuong");
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
