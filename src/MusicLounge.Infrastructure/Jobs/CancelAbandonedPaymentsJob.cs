using Microsoft.EntityFrameworkCore;
using Hangfire;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Enums;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

public sealed class CancelAbandonedPaymentsJob
{
    /// <summary>
    /// Mac dinh khi <c>system_config</c> chua co khoa — xem <see cref="ConfigKeys.PaymentAbandonMinutes"/>
    /// de biet vi sao con so nay phai lon hon cua so retry IPN cua VNPay.
    /// </summary>
    public const int DefaultAbandonMinutes = 60;

    private readonly ApplicationDbContext _ctx;
    private readonly ISystemConfigService _config;

    public CancelAbandonedPaymentsJob(ApplicationDbContext ctx, ISystemConfigService config)
    {
        _ctx = ctx;
        _config = config;
    }

    // MLACP-333. Cho nay truoc day cho 30 phut, dua tren mot comment ghi "VNPay retries the
    // callback for ~15 minutes". Con so 15 phut do SAI: tai lieu chinh chu cua VNPay ghi ro IPN
    // duoc goi lai toi da 10 lan, moi lan cach nhau 5 phut — lan cuoi co the roi vao khoang phut
    // thu 50. Nen bien an toan ma comment cu tuong la +15 phut thuc ra la -20 phut: tu phut 30 den
    // phut 50, VNPay VAN dang retry hop le trong khi ve da bi huy va thanh toan da bi danh Failed.
    // Mot xac nhan thanh cong den trong khoang do se bi coi la callback trung lap va bo di —
    // khach mat tien ma khong co ve, va khong ai tim ra duoc vi khong cho nao doc PaymentStatus.Failed.
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task ExecuteAsync(IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;
        var abandonMinutes = await _config.GetIntAsync(
            ConfigKeys.PaymentAbandonMinutes, DefaultAbandonMinutes, ct);
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-abandonMinutes);

        // Combining the Status equality with the CreatedAt comparison in one Where doesn't
        // translate under the SQLite provider used in tests (same limitation documented
        // repeatedly elsewhere in this codebase, empirically confirmed here by a test that used
        // to throw) — filter by Status server-side, the date client-side.
        var stalePaymentIds = (await _ctx.Payments
                .Where(p => p.Status == PaymentStatus.Pending)
                .Select(p => new { p.Id, p.CreatedAt })
                .ToListAsync(ct))
            .Where(p => p.CreatedAt <= cutoff)
            .Select(p => p.Id)
            .ToList();

        if (stalePaymentIds.Count == 0) return;

        await _ctx.Tickets
            .Where(t => t.PaymentId.HasValue
                && stalePaymentIds.Contains(t.PaymentId.Value)
                && t.Status == TicketStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TicketStatus.Cancelled), ct);

        // A second empirically-confirmed SQLite provider limitation (distinct from the one above):
        // ExecuteUpdateAsync's SetProperty translator chokes on the implicit DateTimeOffset ->
        // DateTimeOffset? conversion needed to assign UtcNow into the nullable UpdatedAt column.
        // Falls back to fetch-then-mutate — the batch here is inherently small (payments stuck
        // >30 minutes), so losing the single-statement bulk UPDATE is not a real cost.
        var stalePayments = await _ctx.Payments
            .Where(p => stalePaymentIds.Contains(p.Id))
            .ToListAsync(ct);
        foreach (var payment in stalePayments)
        {
            payment.Status = PaymentStatus.Failed;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
    }
}
