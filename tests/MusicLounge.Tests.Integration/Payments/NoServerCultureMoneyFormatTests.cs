using System.Text.RegularExpressions;
using FluentAssertions;

namespace MusicLounge.Tests.Integration.Payments;

/// <summary>
/// MLACP-645 — test QUÉT. Câu tiếng Việt in tiền bằng <c>{x:N0}đ</c> lấy dấu ngăn của culture MÁY CHỦ ("783,000đ").
/// Mọi số tiền trong chữ tiếng Việt phải đi qua <c>VietnamMoney.Format</c>. Bài này giữ cho các nhánh viết sau (hoặc đang
/// mở cùng lúc) không đưa lại mẫu cũ — 05/10/2026 khi gộp thử đã gặp hai chỗ như vậy từ nhánh khác.
/// </summary>
public sealed class NoServerCultureMoneyFormatTests
{
    private static string SrcDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "src", "MusicLounge.Application"))) d = d.Parent;
        return d is null ? throw new DirectoryNotFoundException("không tìm thấy thư mục src") : Path.Combine(d.FullName, "src");
    }

    [Fact]
    public void NoVietnameseSentencePrintsMoneyWithTheServersCulture()
    {
        var files = Directory.GetFiles(SrcDir(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.EndsWith("VietnamMoney.cs"))
            .ToList();
        files.Should().HaveCountGreaterThan(200, "the scan must actually read the source tree, not pass on zero files");

        var re = new Regex(@"\{[^{}""]+:N0\}đ");
        var hits = files.SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => re.IsMatch(x.line))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToList();
        hits.Should().BeEmpty("use VietnamMoney.Format(x) — '{x:N0}đ' prints '783,000đ' on an en-US server");
    }
}
