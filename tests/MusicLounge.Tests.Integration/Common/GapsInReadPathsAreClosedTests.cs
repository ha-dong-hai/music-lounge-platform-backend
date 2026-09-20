using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Common;

/// <summary>
/// Năm chỗ "ghi được mà đọc không được": lệnh sửa nhận một trường, nhưng không đường đọc nào trả trường đó
/// về, nên màn hình Sửa gửi lại cái nó không có và giá trị cũ bị xoá âm thầm.
///
/// <para>Mỗi ca ở đây kiểm một chỗ đã lấp. Kiểm qua HTTP chứ không qua lớp ánh xạ: trường phải thật sự
/// ra tới client thì màn hình mới gửi lại được, khai trong DTO thôi chưa đủ.</para>
/// </summary>
[Collection("Integration")]
public sealed class GapsInReadPathsAreClosedTests
{
    private readonly ApiFactory _factory;

    public GapsInReadPathsAreClosedTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }

    [Fact]
    public async Task DanhMucBuoiDienChoAdmin_TraCaMoTaVaCaMucDaTat()
    {
        int idDaTat;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cat = new EventCategory
            {
                Name = $"Danh mục đã tắt {Guid.NewGuid():N}",
                Description = "Mô tả phải đọc lại được, nếu không sửa tên là mất mô tả",
                IsActive = false
            };
            db.Add(cat);
            await db.SaveChangesAsync();
            idDaTat = cat.Id;
        }

        // Danh mục công khai (đúng khi chỉ trả mục đang bật) KHÔNG thấy mục vừa tắt — đó chính là lý do
        // cần một đường đọc riêng cho Admin.
        var guest = _factory.CreateClient();
        var congKhai = Data(await (await guest.GetAsync("/api/v1/catalog/event-categories")).Content.ReadAsStringAsync());
        congKhai.EnumerateArray().Select(c => c.GetProperty("id").GetInt32())
            .Should().NotContain(idDaTat);

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/admin/event-categories");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = Data(await res.Content.ReadAsStringAsync()).EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("id").GetInt32() == idDaTat);
        mine.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "tắt một danh mục xong mà không màn hình nào nhìn thấy nó nữa thì không có đường nào bật lại");
        mine.GetProperty("isActive").GetBoolean().Should().BeFalse();
        mine.GetProperty("description").GetString().Should()
            .Be("Mô tả phải đọc lại được, nếu không sửa tên là mất mô tả");
    }

    [Fact]
    public async Task TheLoaiNhacChoAdmin_TraCaTenTiengAnh()
    {
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var genre = new MusicGenre { Name = $"Nhạc kiểm thử {Guid.NewGuid():N}", NameEn = "Test Genre" };
            db.Add(genre);
            await db.SaveChangesAsync();
            id = genre.Id;
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/admin/genres");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var mine = Data(await res.Content.ReadAsStringAsync()).EnumerateArray()
            .FirstOrDefault(g => g.GetProperty("id").GetInt32() == id);
        mine.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        mine.GetProperty("nameEn").GetString().Should().Be("Test Genre",
            "PUT genres/{id} ghi đè cả NameEn, nên phải đọc lại được, nếu không sửa tên tiếng Việt là mất tên tiếng Anh");
    }

    [Fact]
    public async Task ChiTietBuoiHoaNhac_TraOrderIndexCuaTungNgheSi()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await client.GetAsync($"/api/v1/lounge-shows/{SeedHelper.ShowId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var performers = Data(await res.Content.ReadAsStringAsync()).GetProperty("performers");
        performers.GetArrayLength().Should().BeGreaterThan(0,
            "buổi diễn seed phải có đội hình, nếu không ca này không kiểm được gì");

        // So với GIÁ TRỊ THẬT trong cơ sở dữ liệu, không chỉ kiểm trường có mặt: khai trường trong DTO mà
        // hàm ánh xạ điền 0 thì client vẫn gửi 0 ngược lên và thứ tự vẫn hỏng. Phép kiểm chỉ hỏi "có
        // trường không" sẽ xanh trong đúng trường hợp đó — đã thử và thấy nó xanh thật.
        Dictionary<int, int> thuTuThat;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            thuTuThat = db.Set<Performance>()
                .Where(p => p.LoungeShowId == SeedHelper.ShowId)
                .ToDictionary(p => p.Id, p => p.OrderIndex);
        }
        thuTuThat.Should().NotBeEmpty("không có dòng Performance nào thì ca này không kiểm được gì");

        foreach (var p in performers.EnumerateArray())
        {
            p.TryGetProperty("orderIndex", out var orderIndex).Should().BeTrue(
                "UpdatePerformanceCommand bắt buộc gửi OrderIndex, nên phải đọc lại được — suy từ vị trí " +
                "trong mảng là sai khi số đang lưu không liên tục (0, 5, 10)");

            var performanceId = p.GetProperty("performanceId").GetInt32();
            orderIndex.GetInt32().Should().Be(thuTuThat[performanceId],
                "giá trị trả về phải là thứ tự đang lưu, không phải một số mặc định");
        }
    }

    [Fact]
    public async Task HoSoNguoiDung_TraDanhSachTheLoaiDaLoaiTru()
    {
        var userId = SeedHelper.AudienceId;
        int genreId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var genre = new MusicGenre { Name = $"Thể loại bị loại trừ {Guid.NewGuid():N}" };
            db.Add(genre);
            await db.SaveChangesAsync();
            genreId = genre.Id;

            db.Add(new UserDislikedGenre { UserId = userId, GenreId = genreId });
            await db.SaveChangesAsync();
        }

        try
        {
            var client = _factory.CreateAuthenticatedClient(userId, "Audience");
            var res = await client.GetAsync("/api/v1/me");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var profile = Data(await res.Content.ReadAsStringAsync());
            profile.TryGetProperty("dislikedGenreIds", out var disliked).Should().BeTrue(
                "PUT /me/preferences ghi đè toàn phần và nhận DislikedGenreIds, nên hồ sơ phải trả nó về");
            disliked.EnumerateArray().Select(x => x.GetInt32()).Should().Contain(genreId);
        }
        finally
        {
            // Trả nguyên trạng: dữ liệu seed dùng chung giữa các test, để lại một thể loại bị loại trừ
            // sẽ làm lệch các ca kiểm gợi ý chạy sau.
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = db.Set<UserDislikedGenre>().Where(x => x.UserId == userId && x.GenreId == genreId);
            db.RemoveRange(rows);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task DanhSachUngHoCuaToi_TraPerformerId()
    {
        // Tự tạo một khoản ủng hộ thay vì trông vào dữ liệu seed: nếu danh sách rỗng thì vòng lặp kiểm
        // không chạy lần nào và ca này "xanh" mà chẳng kiểm gì.
        int donationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var donation = new Donation
            {
                DonorUserId = SeedHelper.AudienceId,
                PerformanceId = SeedHelper.PerformanceId,
                Gross = 100_000m,
                Net = 85_000m,
                Status = DonationStatus.PendingOwnerAck,
                Message = "Kiểm thử performerId",
                PaymentConfirmedAt = DateTimeOffset.UtcNow
            };
            db.Add(donation);
            await db.SaveChangesAsync();
            donationId = donation.Id;
        }

        try
        {
            var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience");
            var res = await client.GetAsync("/api/v1/donations/my?pageSize=50");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var data = Data(await res.Content.ReadAsStringAsync());
            var items = data.TryGetProperty("items", out var it) ? it : data;

            var mine = items.EnumerateArray().FirstOrDefault(i => i.GetProperty("id").GetInt32() == donationId);
            mine.ValueKind.Should().NotBe(JsonValueKind.Undefined, "khoản ủng hộ vừa tạo phải có trong danh sách");
            mine.GetProperty("performerId").GetInt32().Should().BeGreaterThan(0,
                "tên nghệ sĩ không dò ngược ra người được — danh sách phải mang cả Id");
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RemoveRange(db.Set<Donation>().Where(d => d.Id == donationId));
            await db.SaveChangesAsync();
        }
    }
}
