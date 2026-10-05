namespace MusicLounge.Application.Settlements;

/// <summary>
/// MLACP-664. Lịch chạy của <c>SettlementReleaseJob</c> — MỘT nguồn cho cả việc đăng ký job (DependencyInjection) lẫn việc
/// báo cho người xem "nền tảng dự kiến chuyển lúc nào" (sao kê ủng hộ công khai). Trước đây lịch nằm thẳng trong lời đăng ký
/// (<c>Cron.Daily()</c>), và sao kê chỉ ghi "chờ kỳ giải ngân" mà không có ngày (chủ dự án 05/10/2026: "chưa thấy thời gian nền
/// tảng sẽ chuyển cho phòng trà").
///
/// <para>Hangfire tính cron theo UTC khi không đặt TimeZone (RecurringJobOptions mặc định), nên "0 0 * * *" là 00:00 UTC =
/// 07:00 giờ Việt Nam. <see cref="NextRunAt"/> giả định đúng lịch hằng ngày lúc nửa đêm UTC — đổi <see cref="Cron"/> sang
/// lịch khác thì phải đổi hàm này (test SettlementReleaseScheduleTests chặn).</para>
/// </summary>
public static class SettlementReleaseSchedule
{
    /// <summary>Hằng ngày 00:00 UTC (= <c>Hangfire.Cron.Daily()</c>).</summary>
    public const string Cron = "0 0 * * *";

    /// <summary>Lần chạy đầu tiên mà job sẽ xét một khoản lên lịch lúc <paramref name="scheduledAt"/>, tính từ
    /// <paramref name="now"/>: job lấy khoản có <c>ScheduledAt &lt;= lúc chạy</c>, nên là nửa đêm UTC đầu tiên không sớm hơn
    /// cả hai mốc.</summary>
    public static DateTimeOffset NextRunAt(DateTimeOffset scheduledAt, DateTimeOffset now)
    {
        var tu = scheduledAt > now ? scheduledAt : now;
        var nuaDem = new DateTimeOffset(tu.UtcDateTime.Date, TimeSpan.Zero);
        return nuaDem < tu ? nuaDem.AddDays(1) : nuaDem;
    }
}
