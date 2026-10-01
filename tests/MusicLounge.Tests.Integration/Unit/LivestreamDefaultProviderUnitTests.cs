using System.Text.Json;
using FluentAssertions;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Unit;

/// <summary>
/// MLACP-514. Livestream chỉ dùng Mux (chủ dự án 01/10/2026). Mặc định có HAI chỗ — giá trị mặc định của
/// <see cref="LivestreamSettings"/> và "Livestream:Provider" trong appsettings.json — và trước đây cả hai là "cloudflare",
/// nên máy nào không đặt Livestream__Provider (máy dev, gói nộp chạy local) âm thầm chạy nhà cung cấp đã bỏ. Khoá cả hai.
/// </summary>
public sealed class LivestreamDefaultProviderUnitTests
{
    [Fact]
    public void MacDinhTrongCode_LaMux()
        => new LivestreamSettings().Provider.Should().Be("mux");

    [Fact]
    public void AppSettingsJson_LaMux()
    {
        var thuMuc = new DirectoryInfo(AppContext.BaseDirectory);
        while (thuMuc is not null && !File.Exists(Path.Combine(thuMuc.FullName, "MusicLounge.sln")))
            thuMuc = thuMuc.Parent;
        thuMuc.Should().NotBeNull("không tìm thấy gốc repo (MusicLounge.sln) từ thư mục chạy test");

        var tep = Path.Combine(thuMuc!.FullName, "src", "MusicLounge.Api", "appsettings.json");
        File.Exists(tep).Should().BeTrue();
        using var doc = JsonDocument.Parse(File.ReadAllText(tep),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        doc.RootElement.GetProperty("Livestream").GetProperty("Provider").GetString().Should().Be("mux");
    }
}
