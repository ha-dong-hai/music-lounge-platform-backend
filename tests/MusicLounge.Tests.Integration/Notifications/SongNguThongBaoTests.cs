using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Application.Auth.Jobs;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Notifications;

/// <summary>
/// MLACP-489. Thông báo trong ứng dụng, push, email và SMS song ngữ.
///
/// Quy tắc đang kiểm: thứ trả TRONG RESPONSE theo Accept-Language của chính request; thứ gửi BẤT ĐỒNG BỘ (push, email,
/// SMS) theo User.PreferredLanguage của người nhận — request lúc tạo thông báo thường là của người KHÁC.
///
/// Mọi test tạo người dùng riêng: sửa PreferredLanguage của tài khoản seed dùng chung là làm test khác đổi kết quả
/// theo thứ tự chạy.
/// </summary>
[Collection("Integration")]
public sealed class SongNguThongBaoTests
{
    private readonly ApiFactory _factory;

    public SongNguThongBaoTests(ApiFactory factory) => _factory = factory;

    private sealed class GhiJobDuocTao : IClientFilter
    {
        public List<Job> Jobs { get; } = [];
        public void OnCreating(CreatingContext filterContext) { lock (Jobs) Jobs.Add(filterContext.Job); }
        public void OnCreated(CreatedContext filterContext) { }
    }

    private async Task<int> TaoNguoiDungAsync(string ngonNgu)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Email = $"songngu-{Guid.NewGuid():N}@example.com",
            FullName = "Người Dùng Song Ngữ",
            Role = UserRole.Audience,
            PreferredLanguage = ngonNgu,
            CreatedAt = DateTime.UtcNow
        };
        db.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<int> TaoThongBaoAsync(int userId, string? titleEn, string? bodyEn)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var n = new Notification
        {
            UserId = userId,
            Type = NotificationType.TicketConfirmed,
            Title = "Đặt vé thành công!",
            Body = "Bạn đã đặt 2 vé thành công.",
            TitleEn = titleEn,
            BodyEn = bodyEn,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Add(n);
        await db.SaveChangesAsync();
        return n.Id;
    }

    private static async Task<string> DocDanhSachAsync(HttpClient client, string? acceptLanguage)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications");
        if (acceptLanguage is not null) req.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return await res.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task DanhSachThongBao_TheoAcceptLanguageCuaRequest()
    {
        // Tài khoản ưa tiếng Việt — để chứng minh danh sách đi theo REQUEST, không theo cài đặt đã lưu.
        var id = await TaoNguoiDungAsync(NgonNgu.Viet);
        await TaoThongBaoAsync(id, "Booking confirmed!", "You have successfully booked 2 tickets.");
        var client = _factory.CreateAuthenticatedClient(id, "Audience");

        var anh = await DocDanhSachAsync(client, "en-US,en;q=0.9,vi;q=0.5");
        anh.Should().Contain("Booking confirmed!").And.NotContain("Đặt vé thành công!");

        var viet = await DocDanhSachAsync(client, "vi");
        viet.Should().Contain("Đặt vé thành công!").And.NotContain("Booking confirmed!");

        var khongHeader = await DocDanhSachAsync(client, null);
        khongHeader.Should().Contain("Đặt vé thành công!", "không yêu cầu ngôn ngữ thì mặc định là tiếng Việt");
    }

    [Fact]
    public async Task DongTaoTruocMLACP489_ChuaCoBanDich_LuiVeTiengViet()
    {
        var id = await TaoNguoiDungAsync(NgonNgu.Anh);
        await TaoThongBaoAsync(id, titleEn: null, bodyEn: null);
        var client = _factory.CreateAuthenticatedClient(id, "Audience");

        var anh = await DocDanhSachAsync(client, "en");

        anh.Should().Contain("Đặt vé thành công!",
            "dòng cũ không có bản tiếng Anh thì phải trả tiếng Việt — một ô trống thì người dùng không làm gì được");
    }

    [Theory]
    [InlineData("en", "Booking confirmed!", "Đặt vé thành công!")]
    [InlineData("vi", "Đặt vé thành công!", "Booking confirmed!")]
    public async Task Push_TheoNgonNguTaiKhoanNguoiNhan(string ngonNguNguoiNhan, string phaiCo, string khongDuocCo)
    {
        var nguoiNhan = await TaoNguoiDungAsync(ngonNguNguoiNhan);
        var ghi = new GhiJobDuocTao();
        GlobalJobFilters.Filters.Add(ghi);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifications.NotifyAsync(
                nguoiNhan, NotificationType.TicketConfirmed,
                new SongNgu("Đặt vé thành công!", "Booking confirmed!"),
                new SongNgu("Bạn đã đặt 2 vé thành công.", "You have successfully booked 2 tickets."));

            var push = ghi.Jobs.Where(j => j.Type == typeof(IFcmService) && (int)j.Args[0]! == nguoiNhan).ToList();
            push.Should().ContainSingle("mỗi thông báo đúng một lần đẩy push");
            ((string)push[0].Args[1]!).Should().Be(phaiCo).And.NotBe(khongDuocCo);
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(ghi);
        }
    }

    [Fact]
    public async Task CauQuaDai_DuocCatTruocKhiLuu()
    {
        // SQLite không ép độ dài cột nên test không thể dựa vào việc lưu bị lỗi — kiểm thẳng độ dài đã lưu. SQL Server
        // thật thì ném lỗi khi lưu, mà nhiều chỗ tạo thông báo nằm trong transaction thanh toán.
        var id = await TaoNguoiDungAsync(NgonNgu.Viet);
        var dai = new string('a', NotificationLimits.BodyMaxLength * 2);

        using (var scope = _factory.Services.CreateScope())
        {
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await notifications.NotifyAsync(
                id, NotificationType.ComplaintUpdate, new SongNgu("Tiêu đề", "Title"), new SongNgu(dai, dai));
            await uow.SaveChangesAsync();
        }

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Notifications.AsNoTracking().SingleAsync(n => n.UserId == id);
        saved.Body.Length.Should().BeLessThanOrEqualTo(NotificationLimits.BodyMaxLength);
        saved.BodyEn!.Length.Should().BeLessThanOrEqualTo(NotificationLimits.BodyMaxLength);
    }

    [Fact]
    public async Task DoiNgonNgu_ChiNhanViHoacEn_VaHoSoTraLai()
    {
        var id = await TaoNguoiDungAsync(NgonNgu.Viet);
        var client = _factory.CreateAuthenticatedClient(id, "Audience");

        (await client.PutAsJsonAsync("/api/v1/me/language", new { preferredLanguage = "fr" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/v1/me/language", new { preferredLanguage = "en-US" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "chỉ đúng hai mã được lưu — mọi nơi gửi đều so với chúng");

        (await client.PutAsJsonAsync("/api/v1/me/language", new { preferredLanguage = "en" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var hoSo = await client.GetStringAsync("/api/v1/me");
        hoSo.Should().Contain("\"preferredLanguage\":\"en\"",
            "có đường ghi thì phải có đường đọc — không thì trang cài đặt ghi đè lựa chọn thật bằng giá trị mặc định");
    }

    [Theory]
    [InlineData("en-GB,en;q=0.9", "en")]
    [InlineData("vi-VN,vi;q=0.9,en;q=0.5", "vi")]
    public async Task DangKy_GhiNhanNgonNguCuaTrang_VaGuiEmailMaBangNgonNguDo(string acceptLanguage, string mong)
    {
        var email = $"dangky-{Guid.NewGuid():N}@example.com";
        var ghi = new GhiJobDuocTao();
        GlobalJobFilters.Filters.Add(ghi);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
            {
                Content = JsonContent.Create(new
                {
                    Email = email, Password = "P@ssword123-safe", FullName = "Người Đăng Ký",
                    Phone = (string?)null, AcceptTerms = true
                })
            };
            req.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
            var res = await _factory.CreateClient().SendAsync(req);
            res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Users.AsNoTracking().SingleAsync(u => u.Email == email)).PreferredLanguage.Should().Be(mong);

            var thu = ghi.Jobs.Single(j => j.Type == typeof(SendEmailVerificationCodeJob) && (string)j.Args[0]! == email);
            thu.Args.Should().Contain(mong, "thư mã xác minh là thư ĐẦU TIÊN người dùng nhận — phải đúng ngôn ngữ trang họ vừa điền");
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(ghi);
        }
    }

    [Fact]
    public void TinSmsTiengAnh_NamGonTrongMotDoanGsm7()
    {
        var noiDung = SmsService.NoiDung("999999", NgonNgu.Anh);

        noiDung.Should().Contain("999999");
        noiDung.All(c => c < 128).Should().BeTrue("ký tự ngoài bảng GSM-7 đẩy tin sang UCS-2 — 70 ký tự một đoạn, tính tiền theo đoạn");
        noiDung.Length.Should().BeLessThanOrEqualTo(160);
        SmsService.NoiDung("999999", NgonNgu.Viet).Should().Be(SmsService.NoiDung("999999"),
            "bản tiếng Việt giữ nguyên câu cũ — các test giới hạn 70 ký tự hiện có vẫn áp cho nó");
    }

    /// <summary>
    /// Bẫy dễ gặp nhất của việc viết 81 chỗ song ngữ bằng tay: dán nhầm câu tiếng Việt vào ô tiếng Anh. Trình biên dịch
    /// không bắt được — hai ô đều là string. Quét mọi <c>new SongNgu(…)</c> trong mã nguồn: phần chữ của ô thứ hai không
    /// được có chữ cái tiếng Việt có dấu.
    /// </summary>
    [Fact]
    public void MoiCauSongNgu_OTiengAnhKhongChuaChuTiengViet()
    {
        var src = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src"));
        var tep = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        tep.Should().NotBeEmpty("quét trúng số không thì test xanh vì không tìm thấy gì");

        var chuViet = new Regex("[àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]",
            RegexOptions.IgnoreCase);
        var soCau = 0;
        var loi = new List<string>();
        foreach (var f in tep)
        {
            var s = File.ReadAllText(f);
            var i = 0;
            while ((i = s.IndexOf("new SongNgu(", i, StringComparison.Ordinal)) >= 0)
            {
                i += "new SongNgu(".Length;
                var doiSo = TachHaiDoiSo(s, i);
                if (doiSo is null) continue;
                soCau++;
                foreach (Match m in Regex.Matches(doiSo.Value.En, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                    if (chuViet.IsMatch(m.Groups[1].Value))
                        loi.Add($"{Path.GetFileName(f)}: {m.Groups[1].Value}");
            }
        }

        soCau.Should().BeGreaterThan(100, "81 chỗ gọi NotifyAsync cộng các cụm câu dùng chung — ít hơn là bộ quét hỏng");
        loi.Should().BeEmpty("ô tiếng Anh đang chứa chữ tiếng Việt — nhiều khả năng dán nhầm");
    }

    /// <summary>Tách hai đối số của new SongNgu( … , … ) ở cấp ngoài cùng; bỏ qua dấu phẩy trong chuỗi và ngoặc.</summary>
    private static (string Vi, string En)? TachHaiDoiSo(string s, int i)
    {
        int depth = 0, batDau = i; string? vi = null; var trongChuoi = false;
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (trongChuoi) { if (c == '\\') { i++; continue; } if (c == '"') trongChuoi = false; continue; }
            if (c == '"') { trongChuoi = true; continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0) return vi is null ? null : (vi, s[batDau..i]);
                depth--;
            }
            else if (c == ',' && depth == 0 && vi is null) { vi = s[batDau..i]; batDau = i + 1; }
        }
        return null;
    }
}
