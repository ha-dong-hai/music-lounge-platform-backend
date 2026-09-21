using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.AiPosters;

/// <summary>
/// MLACP-482. Khi để mô hình TỰ VẼ CHỮ lên poster thì mối nguy KHÔNG phải sai chính tả mà là BỊA SỰ THẬT.
///
/// <para>Đo thật 21/09/2026 với Gemini 3.1 Flash Image, đúng lời nhắc mà hệ thống sinh ra: tiếng Việt có dấu gần như
/// hoàn hảo — "ĐÊM NHẠC TRỊNH", "HẠ TRẮNG", "20:00 – THỨ BẢY, 27/09/2026" đều đúng. Nhưng tấm poster kèm theo một địa
/// chỉ "(20 Ngô Văn Thọ, P.6, Q.3, TP.HCM)", một hotline, một website và một trang mạng xã hội KHÔNG CÁI NÀO CÓ THẬT.
/// Chữ sai dấu thì xấu; địa chỉ bịa thì khách tới nhầm chỗ.</para>
///
/// <para>Thêm danh sách trắng vào lời nhắc thì chạy lại hết sạch thông tin bịa, chữ vẫn đúng dấu. Phép kiểm này khoá
/// câu đó lại — nó dài dòng nên rất dễ bị ai đó "dọn cho gọn".</para>
/// </summary>
[Collection("Integration")]
public sealed class PosterPromptKhongBiaThongTinTests
{
    private readonly ApiFactory _factory;

    public PosterPromptKhongBiaThongTinTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LoiNhacPhaiLietKeRoNhungGiDuocIn_VaCamBiaThongTinLienHe()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);

        var tao = await client.PostAsJsonAsync("/api/v1/lounge-shows", new
        {
            LoungeId = SeedHelper.LoungeId,
            Name = $"DemNhacKiemLoiNhac-{Guid.NewGuid():N}",
            Description = "kiem loi nhac",
            Format = "Offline",
            ScheduledStart = SeedHelper.NextShowStart(),
            ScheduledEnd = (DateTimeOffset?)null,
            CategoryId = (int?)null,
            OfflineQuota = 50,
            OnlineQuota = (int?)null,
            GenreIds = Array.Empty<int>(),
            MoodIds = Array.Empty<int>(),
            AtmosphereIds = Array.Empty<int>(),
            Performances = Array.Empty<object>()
        });
        // Bản đầu của phép kiểm này KHÔNG kiểm bước tạo buổi diễn: nó thiếu hai trường bắt buộc, tạo hỏng, rồi phép
        // kiểm báo "không có dòng nhật ký" — đúng triệu chứng của lỗi khác hẳn. Kiểm ngay tại chỗ hỏng.
        tao.EnsureSuccessStatusCode();
        var showId = (await tao.Content.ReadFromJsonAsync<Bao<int>>())!.Data;

        // Môi trường kiểm thử không cấu hình nhà cung cấp nào, nên lượt này THẤT BẠI — nhưng handler vẫn ghi lại đúng
        // lời nhắc đã dựng, và đó chính là thứ cần soi. Không phải gọi ra ngoài lần nào.
        await client.PostAsJsonAsync($"/api/v1/lounge-shows/{showId}/ai-poster", new { StyleHint = (string?)null });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loiNhac = await db.Set<AiPosterGeneration>()
            .Where(g => g.ShowId == showId)
            .Select(g => g.Prompt)
            .FirstOrDefaultAsync();

        loiNhac.Should().NotBeNullOrEmpty("phải có dòng nhật ký để soi, nếu không phép kiểm này không kiểm gì");

        loiNhac.Should().Contain("Chỉ được in đúng những thông tin đã nêu ở trên",
            "danh sách trắng cụ thể hơn hẳn một câu cấm chung chung — mô hình sinh ảnh hay bỏ qua câu phủ định");
        foreach (var cam in new[] { "địa chỉ", "số điện thoại", "website", "mạng xã hội", "giá vé" })
            loiNhac.Should().Contain(cam, $"phải cấm đích danh '{cam}', đây đều là thứ mô hình đã bịa ra thật");

        loiNhac.Should().NotContain("KHÔNG chứa chữ",
            "đường này CÓ vẽ chữ; câu cấm chữ chỉ dành cho đường ảnh nền + in chữ bằng font");
    }

    private sealed record Bao<T>(T Data);
}
