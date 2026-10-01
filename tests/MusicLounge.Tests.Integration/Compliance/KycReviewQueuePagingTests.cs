using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-505. Hàng đợi xác minh danh tính chuyển từ "nạp đầy đủ cả hàng đợi rồi cắt trang trong bộ nhớ" sang "chỉ nạp
/// đầy đủ người thuộc trang". Đây là thay đổi hiệu năng thuần — kết quả (thứ tự, totalCount) phải GIỐNG HỆT bản cũ.
/// Test tính lại kết quả bằng đúng thuật toán cũ trên cùng dữ liệu rồi so từng trang.
/// </summary>
[Collection("Integration")]
public sealed class KycReviewQueuePagingTests
{
    private readonly ApiFactory _factory;

    public KycReviewQueuePagingTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MoiTrang_GiongHetThuatToanCu_KeCaMocNopTrungNhauVaChuaCoMocNop()
    {
        var goc = DateTimeOffset.UtcNow.AddDays(-30);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // 30 hồ sơ: mốc nộp xáo trộn, 6 cặp trùng mốc, vài người chỉ có hồ sơ thuế, vài người chưa có mốc nộp nào.
            db.Users.AddRange(Enumerable.Range(0, 30).Select(i => new User
            {
                Email = $"k505-{Guid.NewGuid():N}@test.com", FullName = $"Ho so {i}",
                CitizenCardReviewStatus = i % 5 == 0 ? null : KycReviewStatus.Pending,
                CitizenCardSubmittedAt = i % 5 == 0 || i % 7 == 0 ? null : goc.AddHours((i * 37 % 30) / 2),
                TaxProfileReviewStatus = i % 5 == 0 ? KycReviewStatus.Pending : null,
                TaxProfileSubmittedAt = i % 10 == 0 ? null : goc.AddHours(i % 4)
            }));
            await db.SaveChangesAsync();
        }

        // Thuật toán cũ, nguyên văn: lọc → OrderBy (ổn định trên thứ tự DB trả về) → Skip/Take.
        List<Guid> kyVong;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = KycReviewStatus.Pending;
            kyVong = (await db.Users.AsNoTracking()
                    .Where(u => u.CitizenCardReviewStatus == s || u.TaxProfileReviewStatus == s)
                    .ToListAsync())
                .OrderBy(u => u.CitizenCardSubmittedAt ?? u.TaxProfileSubmittedAt ?? DateTimeOffset.MaxValue)
                .Select(u => u.Id)
                .ToList();
        }
        kyVong.Should().HaveCountGreaterThanOrEqualTo(30, "chặn trường hợp quét trúng số không");

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        const int coTrang = 7;
        var thucTe = new List<Guid>();
        for (var trang = 1; (trang - 1) * coTrang < kyVong.Count; trang++)
        {
            var res = await admin.GetAsync($"/api/v1/admin/kyc-reviews?status=Pending&page={trang}&pageSize={coTrang}");
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");
            data.GetProperty("totalCount").GetInt32().Should().Be(kyVong.Count);
            thucTe.AddRange(data.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("userId").GetGuid()));
        }

        thucTe.Should().Equal(kyVong, "thứ tự từng trang phải y hệt bản cũ");
    }
}
