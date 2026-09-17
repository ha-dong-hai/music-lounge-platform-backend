using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-435. Dong cac luot ghep anh nam o Pending qua lau — cung mau voi ExpireStuckDonationsJob.
///
/// StitchVenueTourSceneJob tu dong luot ghep khi gap loi, nhung co nhung duong ma code cua job khong bao gio chay toi:
/// deploy tai cho day thang job sang Failed (da thay voi 2 job dinh ky luc deploy 17/09), job bi xoa tren dashboard, hay
/// tien trinh bi kill giua chung. Khi do luot ghep nam Pending mai mai: chu phong tra cho vo han va luot van bi dem vao
/// gioi han so lan ghep.
///
/// 30 phut: mot luot hop le lau nhat khoang danh thuc dich vu 3 phut + ghep 120 giay + cho Hangfire nhan job + mot lan
/// app khoi dong lai khi deploy (~2 phut) — duoi 10 phut. Neu job con song va chay sau khi bi dong, no thay luot khong con
/// Pending va dung lai; neu no dang chay do va thanh cong, no ghi de thanh Succeeded (ket qua that).
/// </summary>
public sealed class ExpireStuckStitchAttemptsJob
{
    internal static readonly TimeSpan QuaHan = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _ctx;
    private readonly ILogger<ExpireStuckStitchAttemptsJob> _logger;

    public ExpireStuckStitchAttemptsJob(ApplicationDbContext ctx, ILogger<ExpireStuckStitchAttemptsJob> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var cutoff = DateTimeOffset.UtcNow - QuaHan;

        // Loc Status o DB, ngay thang o client — cung gioi han cua provider SQLite trong test nhu ExpireStuckDonationsJob.
        var stuck = (await _ctx.Set<VenueTourStitchAttempt>()
                .Where(a => a.Status == VenueTourStitchStatus.Pending)
                .ToListAsync(ct))
            .Where(a => a.CreatedAt <= cutoff)
            .ToList();

        if (stuck.Count == 0) return;

        foreach (var attempt in stuck)
        {
            attempt.Status = VenueTourStitchStatus.Failed;
            attempt.FailedBySystem = true;
            attempt.ErrorMessage = StitchVenueTourSceneJob.ThongBaoGianDoan;
        }

        await _ctx.SaveChangesAsync(ct);

        // Warning: moi luot bi dong o day la mot luot ma job ghep anh da khong tu ket thuc duoc — dang de nguoi van hanh xem.
        _logger.LogWarning(
            "Đã đóng {Count} lượt ghép ảnh kẹt Pending quá {Minutes} phút: {AttemptIds}",
            stuck.Count, QuaHan.TotalMinutes, string.Join(", ", stuck.Select(a => a.Id)));
    }
}
