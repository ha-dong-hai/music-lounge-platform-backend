using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Tests.Integration.Helpers;
using MusicLoungeVenue = MusicLounge.Domain.Entities.MusicLounge;

namespace MusicLounge.Tests.Integration.Auth;

/// <summary>
/// MLACP-449. Claim <c>lounge_id</c> trước đây chỉ gắn cho Staff; chủ phòng trà nhận <c>null</c> dù phòng trà của họ luôn
/// xác định được (đúng một chủ — một phòng trà). Các test đi qua đăng nhập thật và đọc thẳng claim trong token đã ký.
/// </summary>
[Collection("Integration")]
public sealed class TokenLoungeTests
{
    private const string MatKhau = "P@ssword123-safe";
    private readonly ApiFactory _factory;

    public TokenLoungeTests(ApiFactory factory) => _factory = factory;

    private static string EmailMoi() => $"tl-{Guid.NewGuid():N}@test.com";

    /// <summary>Đăng ký qua API (đúng luồng thật, băm mật khẩu thật), rồi đánh dấu email đã xác thực trong DB.</summary>
    private async Task<int> TaoTaiKhoanAsync(string email, string role)
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email, Password = MatKhau, FullName = "Token Lounge Test", Phone = (string?)null,
            AcceptTerms = true, Role = role
        });
        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> TaoPhongTraAsync(int ownerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lounge = new MusicLoungeVenue
        {
            OwnerId = ownerId, Name = $"TL-{Guid.NewGuid():N}"[..20], Status = LoungeStatus.Approved,
            Address = new VenueAddress { Street = "1 Test St", District = "1", City = "HCM" }
        };
        db.Lounges.Add(lounge);
        await db.SaveChangesAsync();
        return lounge.Id;
    }

    private async Task<JsonElement> DangNhapAsync(string email)
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = MatKhau });
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    /// <summary>Đọc claim lounge_id trong payload JWT (base64url) — đúng thứ server sẽ đọc lại ở request sau.</summary>
    private static string? ClaimLoungeId(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return doc.RootElement.TryGetProperty("lounge_id", out var v) ? v.GetString() : null;
    }

    private static int? LoungeIdTrongPhanHoi(JsonElement data)
        => data.GetProperty("loungeId").ValueKind == JsonValueKind.Null ? null : data.GetProperty("loungeId").GetInt32();

    [Fact]
    public async Task ChuPhongTra_DangNhap_TokenCoLoungeIdCuaPhongTraMinh()
    {
        var email = EmailMoi();
        var ownerId = await TaoTaiKhoanAsync(email, "Owner");
        var loungeId = await TaoPhongTraAsync(ownerId);

        var data = await DangNhapAsync(email);

        LoungeIdTrongPhanHoi(data).Should().Be(loungeId);
        ClaimLoungeId(data.GetProperty("token").GetString()!).Should().Be(loungeId.ToString());
    }

    [Fact]
    public async Task ChuPhongTra_LamMoiToken_VanCoLoungeId()
    {
        // Làm mới token là đường chủ phòng trà vừa tạo phòng trà sẽ đi để nhận claim — phải cùng một kết quả.
        var email = EmailMoi();
        var ownerId = await TaoTaiKhoanAsync(email, "Owner");
        var loungeId = await TaoPhongTraAsync(ownerId);
        var dangNhap = await DangNhapAsync(email);

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new { RefreshToken = dangNhap.GetProperty("refreshToken").GetString() });

        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        LoungeIdTrongPhanHoi(data).Should().Be(loungeId);
        ClaimLoungeId(data.GetProperty("token").GetString()!).Should().Be(loungeId.ToString());
    }

    [Fact]
    public async Task ChuPhongTraChuaCoPhongTra_KhongCoLoungeId()
    {
        var email = EmailMoi();
        await TaoTaiKhoanAsync(email, "Owner");

        var data = await DangNhapAsync(email);

        LoungeIdTrongPhanHoi(data).Should().BeNull();
        ClaimLoungeId(data.GetProperty("token").GetString()!).Should().BeNull();
    }

    [Fact]
    public async Task NhanVien_VanCoLoungeIdNoiDuocPhanCong()
    {
        var email = EmailMoi();
        var staffId = await TaoTaiKhoanAsync(email, "Audience");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Users.SingleAsync(u => u.Id == staffId)).Role = UserRole.Staff;
            db.Set<LoungeStaff>().Add(new LoungeStaff
            {
                UserId = staffId, LoungeId = SeedHelper.LoungeId, IsActive = true, AssignedBy = SeedHelper.OwnerId,
                AssignedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var data = await DangNhapAsync(email);

        LoungeIdTrongPhanHoi(data).Should().Be(SeedHelper.LoungeId);
        ClaimLoungeId(data.GetProperty("token").GetString()!).Should().Be(SeedHelper.LoungeId.ToString());
    }

    [Fact]
    public async Task KhanGia_KhongCoLoungeId()
    {
        var email = EmailMoi();
        await TaoTaiKhoanAsync(email, "Audience");

        var data = await DangNhapAsync(email);

        LoungeIdTrongPhanHoi(data).Should().BeNull();
        ClaimLoungeId(data.GetProperty("token").GetString()!).Should().BeNull();
    }

    /// <summary>
    /// Check "nhân viên được phân công" ở danh sách đơn F&B trước đây chỉ so lounge_id, không kiểm vai trò. Token thật không
    /// sinh được lounge_id cho khán giả, nên đây là phòng thủ — nhưng từ MLACP-449 chủ phòng trà cũng mang lounge_id, và check
    /// này phải nói đúng ý của nó.
    /// </summary>
    [Fact]
    public async Task DonFnb_KhongPhaiNhanVien_CoLoungeIdTrung_VanBiChan()
    {
        var client = _factory.CreateAuthenticatedClient(SeedHelper.AudienceId, "Audience", SeedHelper.LoungeId);

        var res = await client.GetAsync($"/api/v1/fnb-orders?loungeId={SeedHelper.LoungeId}");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
