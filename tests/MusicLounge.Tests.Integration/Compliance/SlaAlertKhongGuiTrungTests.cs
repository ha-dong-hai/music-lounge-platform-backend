using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Jobs;
using MusicLounge.Tests.Integration.Helpers;

namespace MusicLounge.Tests.Integration.Compliance;

/// <summary>
/// MLACP-414. Ba job cảnh báo quá hạn chạy hằng giờ mà không nhớ đã cảnh báo hay chưa, nên mỗi giờ lại sinh thêm một
/// thông báo cho cùng một mục. Trên Azure ngày 16/09 tài khoản Admin có 1.019 thông báo chưa đọc, gần như toàn bộ là
/// cùng một cảnh báo cho khiếu nại #2 — cảnh báo thật bị chìm trong đó. <c>RefundSlaBreachAlertJob</c> đã chống trùng
/// từ MLACP-348; đây là ba job còn lại.
/// </summary>
[Collection("Integration")]
public sealed class SlaAlertKhongGuiTrungTests
{
    private readonly ApiFactory _factory;

    public SlaAlertKhongGuiTrungTests(ApiFactory factory) => _factory = factory;

    private async Task RunAsync<TJob>() where TJob : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<TJob>();
        await ((dynamic)job).ExecuteAsync(new JobCancellationToken(false));
    }

    private async Task<int> CountAlertsAsync(NotificationType type, string referenceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(
            n => n.UserId == SeedHelper.AdminId && n.Type == type && n.ReferenceId == referenceId);
    }

    [Fact]
    public async Task KhieuNaiQuaHan_ChayJobHaiLan_ChiCoMotCanhBao()
    {
        int complaintId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var complaint = new Complaint
            {
                TargetType = "venue",
                TargetId = SeedHelper.LoungeId,
                Category = ComplaintCategory.Other,
                Description = "Chỗ gửi xe quá nhỏ vào tối cuối tuần.",
                Status = ComplaintStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-100),
                SlaDeadline = DateTimeOffset.UtcNow.AddHours(-28)
            };
            db.Add(complaint);
            await db.SaveChangesAsync();
            complaintId = complaint.Id;
        }

        await RunAsync<ComplaintSlaBreachAlertJob>();
        await RunAsync<ComplaintSlaBreachAlertJob>();

        (await CountAlertsAsync(NotificationType.ComplaintUpdate, complaintId.ToString())).Should().Be(1);
    }

    [Fact]
    public async Task DuyetNoiDungQuaHan_ChayJobHaiLan_ChiCoMotCanhBao()
    {
        int moderationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var moderation = new EventModeration
            {
                TargetType = ModerationTargetType.Show,
                TargetId = Random.Shared.Next(100_000, 999_999),
                SlaDeadline = DateTimeOffset.UtcNow.AddHours(-30),
                CreatedAt = DateTime.UtcNow.AddHours(-60)
            };
            db.Add(moderation);
            await db.SaveChangesAsync();
            moderationId = moderation.Id;
        }

        await RunAsync<ModerationSlaBreachAlertJob>();
        await RunAsync<ModerationSlaBreachAlertJob>();

        (await CountAlertsAsync(NotificationType.ModerationSlaBreached, moderationId.ToString())).Should().Be(1);
    }

    [Fact]
    public async Task BaoCaoViPhamQuaHan_ChayJobHaiLan_ChiCoMotCanhBao()
    {
        var targetId = Random.Shared.Next(100_000, 999_999);
        await SeedReportAsync(targetId, hoursAgo: 50);

        await RunAsync<ContentReportSlaBreachAlertJob>();
        await RunAsync<ContentReportSlaBreachAlertJob>();

        (await CountAlertsAsync(NotificationType.ContentReportSlaBreached, $"Show:{targetId}")).Should().Be(1);
    }

    [Fact]
    public async Task BaoCaoViPhamDotMoi_SauKhiDotCu_DaXuLy_VanDuocCanhBaoLai()
    {
        // Cảnh báo theo đích báo cáo chứ không theo từng báo cáo: chống trùng không được biến thành "im lặng vĩnh viễn"
        // cho đích đó. Đợt báo cáo mới sau khi đợt cũ đã xử lý xong vẫn phải được cảnh báo.
        var targetId = Random.Shared.Next(100_000, 999_999);
        var oldReportId = await SeedReportAsync(targetId, hoursAgo: 200);
        await RunAsync<ContentReportSlaBreachAlertJob>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var old = await db.ContentReports.SingleAsync(r => r.Id == oldReportId);
            old.Status = ContentReportStatus.Dismissed;
            // Cảnh báo của đợt cũ đã gửi từ 100 giờ trước — đợt mới bên dưới bắt đầu sau mốc đó, đúng như dòng thời
            // gian thật (test không lùi được đồng hồ nên lùi ngày tạo của thông báo).
            foreach (var alert in await db.Notifications
                         .Where(n => n.ReferenceId == $"Show:{targetId}").ToListAsync())
                alert.CreatedAt = DateTimeOffset.UtcNow.AddHours(-100);
            await db.SaveChangesAsync();
        }
        await SeedReportAsync(targetId, hoursAgo: 50);

        await RunAsync<ContentReportSlaBreachAlertJob>();

        (await CountAlertsAsync(NotificationType.ContentReportSlaBreached, $"Show:{targetId}")).Should().Be(2);
    }

    private async Task<int> SeedReportAsync(int targetId, int hoursAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var report = new ContentReport
        {
            TargetType = ReportTargetType.Show,
            TargetId = targetId,
            ReporterId = SeedHelper.AudienceId,
            Reason = "Nội dung không phù hợp",
            Status = ContentReportStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo)
        };
        db.Add(report);
        await db.SaveChangesAsync();
        return report.Id;
    }
}
