using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// MLACP-427. GET /me trước đây không trả số điện thoại lẫn trạng thái xác minh. Chạy thật luồng SMS qua Twilio ngày
/// 17/09: database đã ghi PhoneVerified = 1 nhưng /me không phản ánh, nên giao diện không hiện được "đã xác minh", không
/// biết ẩn nút "gửi mã", và không biết yêu cầu xác minh lại khi người dùng đổi số.
/// GET /api/v1/me
/// </summary>
[Collection("Integration")]
public sealed class MyProfilePhoneStatusTests
{
    private readonly ApiFactory _factory;

    public MyProfilePhoneStatusTests(ApiFactory factory) => _factory = factory;

    /// <summary>Tài khoản dùng riêng — không động vào tài khoản seed dùng chung.</summary>
    private async Task<int> TaoNguoiDungAsync(string? phone)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"ho-so-{Guid.NewGuid():N}@test.com",
            FullName = "Ho So SDT",
            Role = UserRole.Audience,
            Phone = phone
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<JsonElement> DocHoSoAsync(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/me");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    /// <summary>
    /// Bắt job gửi SMS lúc được tạo, qua bộ lọc toàn cục của Hangfire — đọc thẳng kho lưu trữ không đáng tin vì
    /// BackgroundJob.Enqueue tĩnh bám vào kho có sẵn ở lần gọi đầu tiên. Không so tên lớp job, chỉ lấy theo số điện
    /// thoại, nên đúng dù job được đưa vào hàng đợi qua lớp nào.
    /// </summary>
    private sealed class GhiJobDuocTao : IClientFilter
    {
        public List<Job> Jobs { get; } = [];
        public void OnCreating(CreatingContext filterContext) { lock (Jobs) Jobs.Add(filterContext.Job); }
        public void OnCreated(CreatedContext filterContext) { }
    }

    [Fact]
    public async Task ChuaXacMinh_TraSoDienThoaiVaTrangThaiChuaXacMinh()
    {
        var id = await TaoNguoiDungAsync("0912345678");

        var hoSo = await DocHoSoAsync(_factory.CreateAuthenticatedClient(id, "Audience"));

        hoSo.GetProperty("phone").GetString().Should().Be("0912345678");
        hoSo.GetProperty("phoneVerified").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ChuaCoSoDienThoai_TraNullVaChuaXacMinh()
    {
        var id = await TaoNguoiDungAsync(null);

        var hoSo = await DocHoSoAsync(_factory.CreateAuthenticatedClient(id, "Audience"));

        hoSo.GetProperty("phone").ValueKind.Should().Be(JsonValueKind.Null);
        hoSo.GetProperty("phoneVerified").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task NhapDungMa_HoSoBaoDaXacMinh_DoiSoThiQuayVeChuaXacMinh()
    {
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999);
        var id = await TaoNguoiDungAsync(phone);
        var client = _factory.CreateAuthenticatedClient(id, "Audience");

        // Xin mã thật, rồi lấy mã từ chính job gửi SMS — không tự tính lại mã băm, để test đi đúng luồng người dùng.
        var ghi = new GhiJobDuocTao();
        GlobalJobFilters.Filters.Add(ghi);
        try
        {
            (await client.PostAsync("/api/v1/me/phone/verification-code", null)).StatusCode
                .Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(ghi);
        }

        var job = ghi.Jobs.Single(j => j.Args.Count >= 2 && (j.Args[0] as string) == phone);
        string ma;
        using (var scope = _factory.Services.CreateScope())
            ma = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect((string)job.Args[1]!);

        (await client.PostAsJsonAsync("/api/v1/me/phone/verify", new { Code = ma })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await DocHoSoAsync(client)).GetProperty("phoneVerified").GetBoolean()
            .Should().BeTrue("vừa nhập đúng mã — giao diện phải thấy được để hiện 'đã xác minh'");

        // Đổi số: hệ thống huỷ xác minh (số mới chưa ai chứng minh là của người dùng) — hồ sơ phải phản ánh để giao
        // diện yêu cầu xác minh lại.
        (await client.PutAsJsonAsync("/api/v1/me/profile", new { FullName = "Ho So SDT", Phone = "0987654321", AvatarUrl = (string?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sauKhiDoi = await DocHoSoAsync(client);
        sauKhiDoi.GetProperty("phone").GetString().Should().Be("0987654321");
        sauKhiDoi.GetProperty("phoneVerified").GetBoolean().Should().BeFalse();
    }
}
