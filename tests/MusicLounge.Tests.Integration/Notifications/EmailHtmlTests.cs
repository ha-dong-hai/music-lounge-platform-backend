using System.Net.Mail;
using System.Net.Mime;
using FluentAssertions;
using Hangfire;
using Hangfire.Client;
using Hangfire.Common;
using MediatR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.ValueObjects;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Infrastructure.Persistence;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Services.Email;
using MusicLounge.Infrastructure.Settings;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Notifications;

/// <summary>
/// MLACP-635. Thư theo bộ mẫu đã duyệt: mọi thư có HAI phần (chữ trơn + HTML) dựng từ một khung chung; thư xác nhận vé được
/// xếp sau khi thanh toán online và mang đúng dữ liệu của lần thanh toán đó.
/// </summary>
[Collection("Integration")]
public sealed class EmailHtmlTests
{
    private readonly ApiFactory _factory;
    public EmailHtmlTests(ApiFactory factory) => _factory = factory;

    private sealed class MoiTruong : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record ThuBat(string TieuDe, string? ChuTron, string? Html, string NguoiNhan);

    /// <summary>Dịch vụ thư thật, chỉ thay bước gửi SMTP bằng bắt thư (đọc nội dung ngay, trước khi thư bị huỷ).</summary>
    private static (SmtpEmailService Dv, List<ThuBat> Hop) DichVu()
    {
        var hop = new List<ThuBat>();
        var dv = new SmtpEmailService(Options.Create(new EmailSettings { Host = "", FromAddress = "gui@example.com" }),
            NullLogger<SmtpEmailService>.Instance, new MoiTruong());
        dv.BatThu = m =>
        {
            string? Doc(string loai) => m.AlternateViews.FirstOrDefault(v => v.ContentType.MediaType == loai) is { } v
                ? new StreamReader(v.ContentStream).ReadToEnd() : null;
            hop.Add(new ThuBat(m.Subject, Doc(MediaTypeNames.Text.Plain), Doc(MediaTypeNames.Text.Html), m.To[0].Address));
            return Task.CompletedTask;
        };
        return (dv, hop);
    }

    [Fact]
    public async Task BaThuCu_DeuCoHaiPhan_VaDungNoiDungMau()
    {
        var (dv, hop) = DichVu();
        await dv.SendEmailVerificationCodeAsync("a@example.com", "Lan", "482913", NgonNgu.Viet);
        await dv.SendPasswordResetEmailAsync("a@example.com", "Lan", "https://web.example/reset-password?token=abc", NgonNgu.Viet);
        await dv.SendPerformerConfirmationAsync("b@example.com", "Thiên Di", new SongNgu("Xác nhận lịch diễn", "Confirm your set"),
            new SongNgu("Phòng trà mời bạn xác nhận.", "The venue asks you to confirm."), "https://web.example/performer-confirmation?token=x", DateTimeOffset.UtcNow.AddDays(2));

        hop.Should().HaveCount(3);
        hop.Should().OnlyContain(t => t.ChuTron != null && t.Html != null, "mọi thư gửi hai phần: chữ trơn cho hộp thư không hiện HTML");
        hop.Should().OnlyContain(t => !t.ChuTron!.Contains('<'), "phần chữ trơn không được lẫn thẻ HTML");

        hop[0].TieuDe.Should().Be("Mã xác thực MusicLounge của bạn: 482913");
        hop[0].Html.Should().Contain("482913").And.Contain("Mã có hiệu lực 10 phút");
        hop[1].Html.Should().Contain("href=\"https://web.example/reset-password?token=abc\"").And.Contain("Đặt mật khẩu mới");
        hop[2].TieuDe.Should().Be("Xác nhận lịch diễn / Confirm your set");
        hop[2].ChuTron.Should().Contain("Phòng trà mời bạn xác nhận.").And.Contain("The venue asks you to confirm.");
    }

    [Fact]
    public void ThuNgheSi_KemChungTu_CoTepDinhKem_VaCauChiToiTep_ChiKhiCoTep()
    {
        var thu = new ThuEmail
        {
            NgonNgu = NgonNgu.Viet, TieuDe = "t", XemTruoc = "x", TenNguoiNhan = "Thiên Di", LyDoNhan = "l",
        };
        var anh = new EmailAttachment("chung-tu-chuyen-khoan.png", "image/png", [0x89, 0x50, 0x4E, 0x47]);
        var caiDat = new EmailSettings { FromAddress = "gui@example.com" };

        using (var co = SmtpEmailService.TaoThu(caiDat, "b@example.com", "Thiên Di", "s", thu, anh))
        {
            var tep = co.Attachments.Should().ContainSingle().Subject;
            tep.Name.Should().Be("chung-tu-chuyen-khoan.png");
            tep.ContentType.MediaType.Should().Be("image/png");
            co.AlternateViews.Should().HaveCount(2, "vẫn đủ hai phần chữ trơn + HTML");
        }
        using (var khong = SmtpEmailService.TaoThu(caiDat, "b@example.com", "Thiên Di", "s", thu))
            khong.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task ThuNgheSi_CauChungTu_ChiInKhiThuThatSuCoTep()
    {
        var (dv, hop) = DichVu();
        var anh = new EmailAttachment("chung-tu-chuyen-khoan.jpeg", "image/jpeg", [0xFF, 0xD8, 0xFF]);
        await dv.SendPerformerConfirmationAsync("b@example.com", "Thiên Di", new SongNgu("Đã nhận?", "Received?"),
            new SongNgu("Phòng trà báo đã chuyển.", "The venue reports a transfer."), "https://web.example/x?token=1",
            DateTimeOffset.UtcNow.AddDays(3), anh);
        await dv.SendPerformerConfirmationAsync("b@example.com", "Thiên Di", new SongNgu("Đã nhận?", "Received?"),
            new SongNgu("Phòng trà báo đã chuyển.", "The venue reports a transfer."), "https://web.example/x?token=2",
            DateTimeOffset.UtcNow.AddDays(3));

        hop[0].ChuTron.Should().Contain("Ảnh chứng từ chuyển khoản phòng trà đã nộp được đính kèm thư này")
            .And.Contain("The proof of transfer the venue submitted is attached");
        hop[1].ChuTron.Should().NotContain("đính kèm", "không có tệp thì không được hứa có tệp");
    }

    [Fact]
    public async Task NguoiNhanTiengAnh_NhanThuTiengAnh()
    {
        var (dv, hop) = DichVu();
        await dv.SendEmailVerificationCodeAsync("a@example.com", "Lan", "482913", NgonNgu.Anh);

        hop.Single().TieuDe.Should().Be("Your MusicLounge verification code: 482913");
        hop.Single().Html.Should().Contain("Verify your email").And.NotContain("Xin chào");
    }

    [Fact]
    public void KhungThu_MaHoaChuNguoiDungGo_VaChiMotNut()
    {
        var (html, chuTron) = KhungThu.Dung(new ThuEmail
        {
            NgonNgu = NgonNgu.Viet,
            TieuDe = "Vé của bạn",
            XemTruoc = "Xem trước",
            TenNguoiNhan = "<script>alert(1)</script>",
            Bang = [("Buổi diễn", "Rock & Roll <b>đêm</b>")],
            Nut = new NutThu("Xem vé của tôi", "https://web.example/my-shows/ticket/1?a=1&b=2"),
            LyDoNhan = "Lý do",
        });

        html.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        html.Should().Contain("Rock &amp; Roll &lt;b&gt;đêm&lt;/b&gt;");
        html.Should().Contain("href=\"https://web.example/my-shows/ticket/1?a=1&amp;b=2\"");
        System.Text.RegularExpressions.Regex.Matches(html, "background-color:#1F1A17;\"><a ").Count.Should().Be(1, "mỗi thư một nút chính");
        chuTron.Should().Contain("Buổi diễn: Rock & Roll <b>đêm</b>", "chữ trơn giữ nguyên văn, không mã hoá HTML");
    }

    [Fact]
    public async Task ThuVe_Gom_CaLanThanhToan_VaMoThangVeDau()
    {
        var (paymentId, veDau) = await TaoLanMuaAsync(PaymentStatus.Confirmed, ngonNgu: NgonNgu.Viet);
        var ghi = new GhiThuVe();
        await ChayJobAsync(paymentId, ghi);

        var thu = ghi.Thu.Should().ContainSingle().Subject;
        thu.Language.Should().Be(NgonNgu.Viet);
        thu.OrderCode.Should().StartWith("THU635-");
        thu.Lines.Should().ContainSingle().Which.Should().Be(new TicketConfirmationLine("Bàn gần sân khấu", "Giá chuẩn", 2, 350_000m));
        thu.Total.Should().Be(700_000m);
        thu.Online.Should().BeFalse("vé hạng vào cửa");
        thu.TicketUrl.Should().Be($"http://localhost/my-shows/ticket/{veDau}");

        // Và thư dựng ra đọc được: tiền nguyên đồng kiểu Việt, nút mở đúng vé.
        // Chạy dưới vùng dùng "-" cho ngày (nl-NL) để lỗi định dạng theo vùng lộ ra trên mọi máy, không chỉ máy đặt vùng đó.
        var vungCu = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
        var (dv, hop) = DichVu();
        try { await dv.SendTicketConfirmationAsync(thu); }
        finally { System.Globalization.CultureInfo.CurrentCulture = vungCu; }
        hop.Single().TieuDe.Should().StartWith("Vé của bạn: ");
        hop.Single().ChuTron.Should().Contain("700.000đ").And.Contain("× 2");
        // Ngày theo dạng Việt dd/MM/yyyy, theo giờ Việt Nam — không theo thiết lập vùng của máy chủ.
        hop.Single().ChuTron.Should().Contain($", {VietnamTime.Format(thu.Start, "dd/MM/yyyy")}, ");
        VietnamTime.Format(thu.Start, "dd/MM/yyyy").Should().Contain("/");
    }

    [Fact]
    public async Task ThanhToanChuaXacNhan_KhongGuiThu()
    {
        var (paymentId, _) = await TaoLanMuaAsync(PaymentStatus.Pending, ngonNgu: NgonNgu.Viet);
        var ghi = new GhiThuVe();
        await ChayJobAsync(paymentId, ghi);
        ghi.Thu.Should().BeEmpty();
    }

    [Fact]
    public async Task SuKienThanhToan_XepJobThuVe_TruVeTaiQuay()
    {
        var ghi = new GhiJob();
        GlobalJobFilters.Filters.Add(ghi);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var pub = scope.ServiceProvider.GetRequiredService<IPublisher>();
            var coNguoiMua = Guid.NewGuid();
            await pub.Publish(new TicketPaymentConfirmed(coNguoiMua, SeedHelper.AudienceId, SeedHelper.OwnerId, [Guid.NewGuid()], null, SeedHelper.OfflineShowId));
            await pub.Publish(new TicketPaymentConfirmed(Guid.NewGuid(), Guid.Empty, SeedHelper.OwnerId, [Guid.NewGuid()], null, SeedHelper.OfflineShowId));

            var thuVe = ghi.Jobs.Where(j => j.Type == typeof(SendTicketConfirmationEmailJob)).ToList();
            thuVe.Should().ContainSingle("vé tại quầy (không có người mua) không có ai để gửi thư");
            thuVe[0].Args[0].Should().Be(coNguoiMua);
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(ghi);
        }
    }

    // ---------- dựng dữ liệu ----------

    private sealed class GhiJob : IClientFilter
    {
        public List<Job> Jobs { get; } = [];
        public void OnCreating(CreatingContext filterContext) { lock (Jobs) Jobs.Add(filterContext.Job); }
        public void OnCreated(CreatedContext filterContext) { }
    }

    private sealed class GhiThuVe : IEmailService
    {
        public List<TicketConfirmationEmail> Thu { get; } = [];
        public Task SendTicketConfirmationAsync(TicketConfirmationEmail email, CancellationToken ct = default) { Thu.Add(email); return Task.CompletedTask; }
        public Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink, string language, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string toEmail, string toName, string code, string language, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPerformerConfirmationAsync(string toEmail, string toName, SongNgu subject, SongNgu message, string link, DateTimeOffset expiresAt, EmailAttachment? attachment = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private async Task ChayJobAsync(Guid paymentId, IEmailService email)
    {
        using var scope = _factory.Services.CreateScope();
        var job = new SendTicketConfirmationEmailJob(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), email,
            scope.ServiceProvider.GetRequiredService<IOptions<BusinessSettings>>());
        await job.ExecuteAsync(paymentId, new JobCancellationToken(false));
    }

    /// <summary>Một lần mua 2 vé cùng hạng của buổi tại chỗ, người mua riêng cho test (không sửa tài khoản seed dùng chung).</summary>
    private async Task<(Guid PaymentId, Guid VeDau)> TaoLanMuaAsync(PaymentStatus trangThai, string ngonNgu)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var nguoi = new User { Email = $"thu635-{Guid.NewGuid():N}@example.com", FullName = "Nguyễn Thu Hà", Role = UserRole.Audience, PreferredLanguage = ngonNgu };
        var tier = new TicketTier { LoungeShowId = SeedHelper.OfflineShowId, Name = "Bàn gần sân khấu", AccessType = AccessType.Physical, TotalCapacity = 10 };
        db.AddRange(nguoi, tier);
        await db.SaveChangesAsync();
        var gia = new TicketPrice { TierId = tier.Id, Name = "Giá chuẩn", Price = 350_000m, Quota = 10, IsActive = true };
        var pay = new Payment
        {
            OrderId = $"THU635-{Guid.NewGuid():N}"[..20], PayerId = nguoi.Id, GrossAmount = 700_000m, Status = trangThai,
            ReferenceType = "TicketHold", ReferenceId = Guid.NewGuid().ToString(),
        };
        db.AddRange(gia, pay);
        await db.SaveChangesAsync();

        var ve = Enumerable.Range(0, 2).Select(_ => new Ticket
        {
            BuyerId = nguoi.Id, PriceId = gia.Id, TierId = tier.Id, ShowId = SeedHelper.OfflineShowId, PaymentId = pay.Id,
            Status = TicketStatus.Confirmed, PurchaseChannel = PurchaseChannel.Online, CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();
        db.AddRange(ve);
        await db.SaveChangesAsync();
        return (pay.Id, ve.OrderBy(v => v.Id).First().Id);
    }
}
