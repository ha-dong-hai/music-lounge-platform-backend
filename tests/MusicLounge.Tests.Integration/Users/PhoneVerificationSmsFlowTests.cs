using System.Net;
using System.Reflection;
using FluentAssertions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Users;

/// <summary>
/// MLACP-426. Đoạn nối từ API tới hàng đợi gửi SMS. Trước task này luồng xác minh số điện thoại không có test nào.
/// POST /api/v1/me/phone/verification-code
/// </summary>
[Collection("Integration")]
public sealed class PhoneVerificationSmsFlowTests
{
    private readonly ApiFactory _factory;

    public PhoneVerificationSmsFlowTests(ApiFactory factory) => _factory = factory;

    /// <summary>Tài khoản dùng riêng — không động vào tài khoản seed dùng chung (xem DataErasureTests).</summary>
    private async Task<(int Id, string Phone)> TaoNguoiDungCoSoDienThoaiAsync()
    {
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"sms-{Guid.NewGuid():N}@test.com",
            FullName = "Nguoi Nhan SMS",
            Role = UserRole.Audience,
            Phone = phone,
            PhoneVerified = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, phone);
    }

    /// <summary>
    /// Ghi lai moi job duoc tao. Doc thang kho luu tru KHONG dang tin: BackgroundJob.Enqueue tinh bam vao kho co san o
    /// lan goi dau tien trong tien trinh, con JobStorage.Current bi thay moi moi lan mot test dung host rieng — do that,
    /// job vao mot kho con test doc mot kho khac. Bo loc toan cuc thi chay voi moi client (da kiem bang reflection tren
    /// Hangfire.Core 1.8.17: GlobalJobFilters.Filters.Add/Remove, IClientFilter.OnCreating co Job.Type/Args).
    /// </summary>
    private sealed class GhiJobDuocTao : IClientFilter
    {
        public List<Job> Jobs { get; } = [];
        public void OnCreating(CreatingContext filterContext) { lock (Jobs) Jobs.Add(filterContext.Job); }
        public void OnCreated(CreatedContext filterContext) { }
    }

    [Fact]
    public async Task YeuCauMa_DuaJobCoGioiHanThuLaiVaoHangDoi()
    {
        var (id, phone) = await TaoNguoiDungCoSoDienThoaiAsync();
        var client = _factory.CreateAuthenticatedClient(id, "Audience");
        var ghi = new GhiJobDuocTao();
        GlobalJobFilters.Filters.Add(ghi);
        try
        {
            var res = await client.PostAsync("/api/v1/me/phone/verification-code", null);

            res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());
            ghi.Jobs.Should().Contain(j => j.Type == typeof(PhoneVerificationSmsJob) && (string)j.Args[0]! == phone,
                "job gửi SMS phải là lớp có giới hạn thử lại — đưa thẳng SendPhoneVerificationCodeJob vào hàng đợi thì " +
                "Hangfire thử lại mặc định 10 lần kéo dài nhiều giờ, gửi mã đã hết hạn và tốn tiền SMS");
            ghi.Jobs.Where(j => (j.Args.FirstOrDefault() as string) == phone)
                .SelectMany(j => j.Args.Skip(1).OfType<string>())
                .Should().NotContain(a => System.Text.RegularExpressions.Regex.IsMatch(a, @"^\d{6}$"),
                    "mã OTP chỉ được vào hàng đợi ở dạng đã mã hoá, không phải 6 chữ số dạng rõ");
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(ghi);
        }
    }

    [Fact]
    public void GioiHanThuLai_ChiGuiKhiMaConHan()
    {
        var retry = typeof(PhoneVerificationSmsJob).GetCustomAttribute<AutomaticRetryAttribute>();
        retry.Should().NotBeNull("không có thuộc tính này thì Hangfire dùng mặc định 10 lần");

        // Thời hạn mã lấy thẳng từ handler bằng reflection — đổi thời hạn ở đó mà quên chỉnh số lần thử lại thì test đỏ.
        var codeLifetime = (TimeSpan)typeof(Application.DependencyInjection).Assembly
            .GetType("MusicLounge.Application.Users.Commands.RequestPhoneVerification.RequestPhoneVerificationCommandHandler")!
            .GetField("CodeLifetime", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        // Công thức giãn cách lấy nguyên văn từ mã nguồn Hangfire (AutomaticRetryAttribute.DefaultDelayInSecondsByAttemptFunc):
        //   (attempt - 1)^4 + 15 + random.Next(30) * attempt   — lấy trường hợp xấu nhất random = 29.
        // Cộng thêm mỗi lần chạy có thể chờ trọn timeout HTTP 30 giây (lần đầu + mọi lần thử lại).
        var treNhat = Enumerable.Range(1, retry!.Attempts).Sum(a => Math.Pow(a - 1, 4) + 15 + 29 * a)
                      + (retry.Attempts + 1) * 30;

        TimeSpan.FromSeconds(treNhat).Should().BeLessThan(codeLifetime,
            "lần gửi muộn nhất vẫn phải tới tay người dùng trước khi mã hết hạn");
    }
}
