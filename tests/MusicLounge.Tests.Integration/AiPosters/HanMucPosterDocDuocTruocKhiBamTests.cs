using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-483: chủ phòng trà phải biết còn bao nhiêu poster TRƯỚC khi bấm.
///
/// <para>Trước đây <c>remainingThisMonth</c> chỉ có trong câu trả lời của chính lần bấm, còn gói thuê bao chỉ cho biết
/// TRẦN. Nghĩa là muốn đọc số còn lại thì phải TIÊU MỘT LƯỢT — mà lượt đó tốn tiền thật.</para>
///
/// <para>Đếm theo ĐÚNG luật mà lệnh tạo poster dùng (<c>AiPosterQuota</c>), không chép lại: hai nơi trôi ra khỏi nhau
/// thì màn hình báo "còn 3" trong khi máy chủ từ chối vì đã hết.</para>
/// </summary>
[Collection("Integration")]
public sealed class HanMucPosterDocDuocTruocKhiBamTests
{
    private readonly ApiFactory _factory;

    public HanMucPosterDocDuocTruocKhiBamTests(ApiFactory factory) => _factory = factory;

    /// <summary>Dấu riêng để dọn đúng những dòng phép kiểm này dựng ra, không đụng dòng của ai khác.</summary>
    private const string DauRieng = "MLACP-483-kiem-han-muc";

    private async Task ThemLuotAsync(AiPosterGenerationStatus status, int soLuong = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (var i = 0; i < soLuong; i++)
            db.Add(new AiPosterGeneration
            {
                ShowId = SeedHelper.ShowId,
                OwnerId = SeedHelper.OwnerId,
                Status = status,
                Prompt = DauRieng,
                Provider = "kiem-thu",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Trả nguyên trạng. Hạn mức poster tính theo CHỦ PHÒNG TRÀ dùng chung giữa các lớp kiểm thử, nên để lại vài lượt
    /// "đã dùng" là làm cạn hạn mức và các lớp chạy sau sẽ đỏ vì một lý do hoàn toàn không liên quan tới chúng.
    /// </summary>
    private async Task DonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cua = db.Set<AiPosterGeneration>().Where(g => g.Prompt == DauRieng);
        db.RemoveRange(cua);
        await db.SaveChangesAsync();
    }

    private async Task<(int daDung, int conLai, int tran)> DocGoiAsync()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await owner.GetAsync("/api/v1/subscriptions/my");
        res.EnsureSuccessStatusCode();

        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var d = root.TryGetProperty("data", out var x) ? x : root;
        return (d.GetProperty("aiPostersUsedThisMonth").GetInt32(),
                d.GetProperty("aiPostersRemainingThisMonth").GetInt32(),
                d.GetProperty("maxAiPostersPerMonthSnapshot").GetInt32());
    }

    [Fact]
    public async Task LuotThanhCong_LamGiamSoConLai()
    {
        var truoc = await DocGoiAsync();
        try
        {
            await ThemLuotAsync(AiPosterGenerationStatus.Succeeded);

            var sau = await DocGoiAsync();
            sau.daDung.Should().Be(truoc.daDung + 1);
            sau.conLai.Should().Be(truoc.conLai - 1);
            sau.tran.Should().Be(truoc.tran, "trần là của gói, không đổi theo số đã dùng");
        }
        finally { await DonAsync(); }
    }

    [Fact]
    public async Task DonDangCho_CUNG_ChiemMotSuat()
    {
        // Không giữ chỗ thì bấm liên tục trong lúc chờ sẽ vượt trần.
        var truoc = await DocGoiAsync();
        try
        {
            await ThemLuotAsync(AiPosterGenerationStatus.Queued);
            (await DocGoiAsync()).daDung.Should().Be(truoc.daDung + 1);
        }
        finally { await DonAsync(); }
    }

    [Fact]
    public async Task LuotHONG_KHONG_ChiemSuat()
    {
        // Lỗi của nhà cung cấp thì không được tính vào tiền người ta đã trả.
        var truoc = await DocGoiAsync();
        try
        {
            await ThemLuotAsync(AiPosterGenerationStatus.Failed);
            await ThemLuotAsync(AiPosterGenerationStatus.Expired);

            var sau = await DocGoiAsync();
            sau.daDung.Should().Be(truoc.daDung, "Failed và Expired phải tự trả lại lượt");
            sau.conLai.Should().Be(truoc.conLai);
        }
        finally { await DonAsync(); }
    }

    [Fact]
    public async Task SoConLai_KhongBaoGioAm()
    {
        var goi = await DocGoiAsync();
        try
        {
            await ThemLuotAsync(AiPosterGenerationStatus.Succeeded, goi.tran + 3);

            var sau = await DocGoiAsync();
            sau.conLai.Should().Be(0, "dùng quá trần thì hiện 0, không hiện số âm");
            sau.daDung.Should().BeGreaterThan(sau.tran);
        }
        finally { await DonAsync(); }
    }
}
