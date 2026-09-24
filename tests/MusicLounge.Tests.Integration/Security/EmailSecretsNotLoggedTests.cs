using MusicLounge.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MusicLounge.Infrastructure.Services;
using MusicLounge.Infrastructure.Settings;

namespace MusicLounge.Tests.Integration.Security;

/// <summary>
/// MLACP-429. Khi chưa cấu hình SMTP, SmtpEmailService không gửi email mà ghi ra log — kèm luôn link đặt lại mật khẩu,
/// mã xác minh email và link xác nhận của nghệ sĩ ở dạng rõ. Ai đọc được log là chiếm được tài khoản. Cùng kiểu lỗi với
/// bản giả SMS đã sửa ở MLACP-426. Nhánh này có chủ đích cho dev (không có SMTP vẫn lấy được mã để thử) và test đang đọc
/// link từ log, nên chỉ được giữ ở Development và Testing.
/// </summary>
public sealed class EmailSecretsNotLoggedTests
{
    private const string ResetLink = "https://musiclounge.test/reset?token=TOKEN-BI-MAT-123";
    private const string Code = "731942";
    private const string ConfirmLink = "https://musiclounge.test/performer/confirm?token=XAC-NHAN-BI-MAT-456";
    private const string Email = "nguoidung.that@gmail.com";

    private sealed class MoiTruong(string ten) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = ten;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class GhiLog : ILogger<SmtpEmailService>
    {
        public List<(LogLevel Muc, string NoiDung)> Ban { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var noiDung = formatter(state, exception);
            if (state is IReadOnlyList<KeyValuePair<string, object?>> giaTri)
                noiDung += " | " + string.Join(", ", giaTri.Select(g => $"{g.Key}={g.Value}"));
            Ban.Add((logLevel, noiDung));
        }
    }

    private static async Task<GhiLog> GuiCaBaLoaiAsync(string moiTruong)
    {
        var log = new GhiLog();
        var service = new SmtpEmailService(Options.Create(new EmailSettings { Host = "" }), log, new MoiTruong(moiTruong));

        await service.SendPasswordResetEmailAsync(Email, "Nguoi Dung", ResetLink, NgonNgu.Viet);
        await service.SendEmailVerificationCodeAsync(Email, "Nguoi Dung", Code, NgonNgu.Viet);
        await service.SendPerformerConfirmationAsync(Email, "Nghe Si", new SongNgu("Xac nhan", "Confirm"), new SongNgu("Noi dung", "Content"), ConfirmLink, DateTimeOffset.UtcNow.AddDays(1));
        return log;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ChayThatThieuSmtp_KhongGhiBiMatRaLog(string moiTruong)
    {
        var log = await GuiCaBaLoaiAsync(moiTruong);

        log.Ban.Should().NotContain(b => b.NoiDung.Contains("TOKEN-BI-MAT-123"), "link đặt lại mật khẩu cho phép chiếm tài khoản");
        log.Ban.Should().NotContain(b => b.NoiDung.Contains(Code));
        log.Ban.Should().NotContain(b => b.NoiDung.Contains("XAC-NHAN-BI-MAT-456"));
        log.Ban.Should().NotContain(b => b.NoiDung.Contains(Email), "email người nhận là dữ liệu cá nhân — chỉ ghi bản đã che");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task ChayThatThieuSmtp_VanBaoLoiMucError(string moiTruong)
    {
        // Người dùng đang chờ một email sẽ không bao giờ tới — phải là Error, không được im lặng.
        var log = await GuiCaBaLoaiAsync(moiTruong);

        log.Ban.Where(b => b.Muc == LogLevel.Error).Should().HaveCount(3);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task DevVaTest_VanLayDuocMaTuLog(string moiTruong)
    {
        // Giữ nguyên hợp đồng cũ: lập trình viên không có SMTP vẫn lấy được mã để thử, và 3 bộ test đang đọc link từ log.
        var log = await GuiCaBaLoaiAsync(moiTruong);

        log.Ban.Should().Contain(b => b.NoiDung.Contains("TOKEN-BI-MAT-123"));
        log.Ban.Should().Contain(b => b.NoiDung.Contains(Code));
        log.Ban.Should().Contain(b => b.NoiDung.Contains("XAC-NHAN-BI-MAT-456"));
    }
}
