using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Admin;

/// <summary>
/// Hai hàng đợi duyệt của Admin phải LIỆT KÊ được, không chỉ duyệt được.
///
/// <para>Hệ thống có sẵn <c>POST /admin/bank-accounts/{id}/review</c> và
/// <c>POST /venue-penalties/{id}/appeal/review</c>, nhưng không có đường nào liệt kê để biết <c>{id}</c> là
/// gì: tài khoản ngân hàng chưa xác minh không có endpoint nào trả về, còn kháng nghị thì chỉ có
/// <c>GET /venue-penalties/mine</c> lọc theo chính chủ phòng trà đang đăng nhập. Hậu quả không phải là bất
/// tiện: tiền quyết toán không chuyển đi được cho tới khi có người tra tay trong cơ sở dữ liệu, còn kháng
/// nghị để quá hạn thì được duyệt tự động.</para>
///
/// <para>Test dựng dữ liệu riêng của mình chứ không sửa dữ liệu seed dùng chung — seed đang có sẵn các
/// tài khoản ĐÃ xác minh, và đổi trạng thái của chúng sẽ làm hỏng các test khác theo thứ tự chạy.</para>
/// </summary>
[Collection("Integration")]
public sealed class ReviewQueuesAreListableTests
{
    private readonly ApiFactory _factory;

    public ReviewQueuesAreListableTests(ApiFactory factory) => _factory = factory;

    private static JsonElement Data(string body)
    {
        var root = JsonDocument.Parse(body).RootElement;
        return root.TryGetProperty("data", out var d) ? d : root;
    }

    private static JsonElement Items(JsonElement data)
        => data.TryGetProperty("items", out var items) ? items : data;

    [Fact]
    public async Task HangDoiTaiKhoanNhanTien_TraVeTaiKhoanChuaXacMinh_VaCheSoTaiKhoan()
    {
        int accountId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            var account = new BankAccount
            {
                OwnerType = BankAccountOwnerType.Lounge,
                OwnerId = SeedHelper.LoungeId,
                BankName = "Ngân hàng Kiểm Thử",
                AccountNumber = pii.Encrypt("1234567890123"),
                AccountHolder = "Nguyen Van Cho Duyet",
                IsDefault = false,
                IsVerified = false
            };
            db.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/admin/bank-accounts?pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = Items(Data(await res.Content.ReadAsStringAsync()));
        var mine = items.EnumerateArray().FirstOrDefault(i => i.GetProperty("id").GetInt32() == accountId);
        mine.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "tài khoản chưa xác minh phải nằm trong hàng đợi, nếu không Admin không có {id} để gọi lệnh duyệt");

        mine.GetProperty("loungeId").GetInt32().Should().Be(SeedHelper.LoungeId);
        mine.GetProperty("accountHolder").GetString().Should().Be("Nguyen Van Cho Duyet");

        // Che số tài khoản: quyết định xác minh dựa vào TÊN chủ tài khoản, không cần số đầy đủ nằm lại
        // trong trình duyệt của mọi người duyệt.
        var masked = mine.GetProperty("accountNumberMasked").GetString();
        masked.Should().NotBe("1234567890123", "số tài khoản không được trả về đầy đủ");
        masked.Should().EndWith("0123", "phải còn bốn số cuối để đối chiếu");
        mine.GetProperty("accountNumberUnreadable").GetBoolean().Should().BeFalse();

        // Ba điều kiện mà lệnh duyệt sẽ kiểm lại — có mặt để người duyệt thấy trước khi bấm.
        mine.TryGetProperty("holderNameMatches", out _).Should().BeTrue();
        mine.TryGetProperty("ownerIdentityApproved", out _).Should().BeTrue();
        mine.TryGetProperty("expectedAccountHolder", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HangDoiTaiKhoanNhanTien_KhongLietKeTaiKhoanCuaNgheSi()
    {
        // Lệnh duyệt từ chối tài khoản của nghệ sĩ ("do chính nghệ sĩ xác nhận qua liên kết gửi email"),
        // nên liệt kê ra đây chỉ tạo những dòng mà bấm vào là lỗi.
        int performerAccountId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pii = scope.ServiceProvider.GetRequiredService<IPiiEncryptionService>();
            var account = new BankAccount
            {
                OwnerType = BankAccountOwnerType.Performer,
                OwnerId = SeedHelper.PerformerId,
                BankName = "Ngân hàng Kiểm Thử",
                AccountNumber = pii.Encrypt("9999999999"),
                AccountHolder = "Nghe Si Chua Xac Minh",
                IsDefault = false,
                IsVerified = false
            };
            db.Add(account);
            await db.SaveChangesAsync();
            performerAccountId = account.Id;
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/admin/bank-accounts?pageSize=50");
        var items = Items(Data(await res.Content.ReadAsStringAsync()));

        items.EnumerateArray().Select(i => i.GetProperty("id").GetInt32())
            .Should().NotContain(performerAccountId,
                "tài khoản của nghệ sĩ không duyệt ở đây; đưa vào hàng đợi là mời Admin bấm để nhận lỗi");
    }

    [Fact]
    public async Task HangDoiTaiKhoanNhanTien_KhongPhaiAdmin_Tra403()
    {
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await owner.GetAsync("/api/v1/admin/bank-accounts");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HangDoiKhangNghi_TraVeKhangNghiChuaXuLy_VaKhongTraCaiDaXuLy()
    {
        int dangCho, daXuLy;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTimeOffset.UtcNow;

            var chuaXuLy = new VenuePenalty
            {
                LoungeId = SeedHelper.LoungeId,
                PenaltyType = PenaltyType.Warning,
                Reason = "Kiểm thử hàng đợi kháng nghị",
                IssuedBy = SeedHelper.AdminId,
                IssuedAt = now.AddDays(-3),
                EffectiveAt = now.AddDays(-3),
                Status = PenaltyStatus.Active,
                AppealDeadline = now.AddDays(4),
                AppealedAt = now.AddDays(-1),
                AppealReason = "Chúng tôi đã khắc phục ngay trong đêm"
            };
            var xongRoi = new VenuePenalty
            {
                LoungeId = SeedHelper.LoungeId,
                PenaltyType = PenaltyType.Warning,
                Reason = "Kiểm thử kháng nghị đã xử lý",
                IssuedBy = SeedHelper.AdminId,
                IssuedAt = now.AddDays(-10),
                EffectiveAt = now.AddDays(-10),
                Status = PenaltyStatus.Active,
                AppealDeadline = now.AddDays(-3),
                AppealedAt = now.AddDays(-8),
                AppealReason = "Kháng nghị cũ",
                AppealResult = "Upheld",
                ReviewedBy = SeedHelper.AdminId,
                ReviewedAt = now.AddDays(-7)
            };
            db.AddRange(chuaXuLy, xongRoi);
            await db.SaveChangesAsync();
            dangCho = chuaXuLy.Id;
            daXuLy = xongRoi.Id;
        }

        var admin = _factory.CreateAuthenticatedClient(SeedHelper.AdminId, "Admin");
        var res = await admin.GetAsync("/api/v1/venue-penalties/appeals?pageSize=50");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var ids = Items(Data(await res.Content.ReadAsStringAsync()))
            .EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();

        ids.Should().Contain(dangCho,
            "kháng nghị chưa có quyết định phải nằm trong hàng đợi, nếu không Admin không biết để xử lý");
        ids.Should().NotContain(daXuLy, "kháng nghị đã có quyết định không còn là việc đang chờ");

        // Tra lại phần đã xử lý khi cần: cùng endpoint, đổi tham số.
        var resolved = await admin.GetAsync("/api/v1/venue-penalties/appeals?resolved=true&pageSize=50");
        var resolvedIds = Items(Data(await resolved.Content.ReadAsStringAsync()))
            .EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
        resolvedIds.Should().Contain(daXuLy);
        resolvedIds.Should().NotContain(dangCho);
    }

    [Fact]
    public async Task HangDoiKhangNghi_ChuPhongTra_Tra403()
    {
        // Hàng đợi này là việc của Admin; chủ phòng trà xem án phạt của mình qua /venue-penalties/mine.
        var owner = _factory.CreateAuthenticatedClient(SeedHelper.OwnerId, "Owner", SeedHelper.LoungeId);
        var res = await owner.GetAsync("/api/v1/venue-penalties/appeals");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
