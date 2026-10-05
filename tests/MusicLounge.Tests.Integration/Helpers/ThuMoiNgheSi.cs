using Hangfire.Client;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;
using MusicLounge.Application.Performers.Jobs;

namespace MusicLounge.Tests.Integration.Helpers;

/// <summary>
/// MLACP-642. Thư mời nghệ sĩ tự xác nhận nay được XẾP HÀNG (Hangfire) thay vì gửi ngay trong lệnh. Bộ test không chạy
/// máy chủ Hangfire (ApiFactory: "no background processing in tests"), nên không có gì gửi thư — các bài đọc liên kết từ
/// log của SmtpEmailService sẽ không thấy gì. Lớp này bắt job thư mời lúc được tạo (cùng cách bắt job qua
/// <c>GlobalJobFilters</c> như các bài khác) và <see cref="GuiHet"/> chạy chúng đúng như Hangfire sẽ chạy: dựng
/// <see cref="SendPerformerConfirmationEmailJob"/> từ DI rồi gọi với đúng tham số đã xếp hàng.
/// Bộ lọc được đăng ký một lần cho cả bộ test, và chỉ giữ job thư mời — không đụng job khác.
/// </summary>
public static class ThuMoiNgheSi
{
    private static readonly BoLoc Loc = new();
    private static int _daDangKy;

    public static void DangKy()
    {
        if (Interlocked.Exchange(ref _daDangKy, 1) == 0) Hangfire.GlobalJobFilters.Filters.Add(Loc);
    }

    /// <summary>Gửi mọi thư mời đang xếp hàng (theo thứ tự xếp).</summary>
    /// <summary>MLACP-673: các job thư mời đang chờ, để bài test kiểm phương thức + tham số đã xếp.</summary>
    public static IReadOnlyList<Job> DangCho() { lock (Loc.Jobs) return [.. Loc.Jobs]; }

    public static void GuiHet(IServiceProvider services)
    {
        List<Job> ds;
        lock (Loc.Jobs) { ds = [.. Loc.Jobs]; Loc.Jobs.Clear(); }
        foreach (var j in ds)
        {
            using var scope = services.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<SendPerformerConfirmationEmailJob>();
            // MLACP-673: gọi ĐÚNG phương thức đã xếp (thư thường hay thư kèm chứng từ) với đúng tham số — như Hangfire.
            ((Task)j.Method.Invoke(job, [.. j.Args])!).GetAwaiter().GetResult();
        }
    }

    private sealed class BoLoc : IClientFilter
    {
        public readonly List<Job> Jobs = [];
        public void OnCreating(CreatingContext filterContext)
        {
            if (filterContext.Job.Type == typeof(SendPerformerConfirmationEmailJob)) lock (Jobs) Jobs.Add(filterContext.Job);
        }
        public void OnCreated(CreatedContext filterContext) { }
    }
}
