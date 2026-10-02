using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// MLACP-520 (M-449). Mô tả tham số hiện nguyên văn ở Admin > Cấu hình hệ thống, mà 29/32 mô tả seed từng là tiếng
/// Anh — người dùng màn đó là Admin người Việt, và backend chưa có i18n cho dữ liệu (từ điển song ngữ MLACP-487 chỉ
/// dịch thông báo lỗi). Test này chặn khoá mới được seed kèm mô tả tiếng Anh.
/// </summary>
[Collection("Integration")]
public sealed class SystemConfigDescriptionLanguageTests
{
    private const string DauTiengViet = "àáảãạâầấẩẫậăằắẳẵặđèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵ";

    private readonly ApiFactory _factory;

    public SystemConfigDescriptionLanguageTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MoiMoTaThamSo_VietBangTiengViet()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var moTa = await db.SystemConfigs.AsNoTracking()
            .Where(c => c.Description != null)
            .Select(c => new { c.ConfigKey, c.Description })
            .ToListAsync();

        moTa.Should().NotBeEmpty("phép kiểm phải chạm ít nhất một tham số, nếu không nó xanh vì không thấy gì");

        moTa.Where(c => !c.Description!.ToLowerInvariant().Any(ch => DauTiengViet.Contains(ch)))
            .Select(c => $"{c.ConfigKey}: {c.Description}")
            .Should().BeEmpty("mô tả hiện thẳng cho Admin người Việt — viết tiếng Việt có dấu");
    }
}
