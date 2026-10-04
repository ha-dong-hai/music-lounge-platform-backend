using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Application.Common;
using MusicLounge.Infrastructure.Persistence;

namespace MusicLounge.Infrastructure.Jobs;

/// <summary>
/// MLACP-635. Gửi thư xác nhận vé cho MỘT lần thanh toán online (mọi vé của lần đó trong một thư).
///
/// Vì sao là job nền mà không gửi ngay trong callback VNPay: gửi thư là gọi ra ngoài (SMTP) — chậm, có thể hỏng. Callback
/// thanh toán không được chậm hay ném lỗi vì một bức thư (CLAUDE.md: không throw bên trong luồng thanh toán); job thì
/// Hangfire tự thử lại khi SMTP lỗi tạm thời.
///
/// Đọc lại dữ liệu lúc chạy, không mang theo trong tham số job: tham số job nằm nguyên văn trong bảng Hangfire — chỉ để
/// một mã thanh toán ở đó, không để email hay tên người mua.
///
/// TRẦN: không đính ảnh QR vào thư (dự án chưa có thư viện vẽ QR ở backend; vé dùng mã QR hiện trong trang Vé của tôi).
/// Đường nâng cấp: thêm thư viện QR, nhúng ảnh qua LinkedResource (cid:) vào phần HTML.
/// </summary>
public sealed class SendTicketConfirmationEmailJob
{
    private readonly ApplicationDbContext _ctx;
    private readonly IEmailService _email;
    private readonly BusinessSettings _business;

    public SendTicketConfirmationEmailJob(ApplicationDbContext ctx, IEmailService email, IOptions<BusinessSettings> business)
    {
        _ctx = ctx;
        _email = email;
        _business = business.Value;
    }

    [AutomaticRetry(Attempts = 5)]
    public async Task ExecuteAsync(Guid paymentId, IJobCancellationToken cancellationToken)
    {
        var ct = cancellationToken.ShutdownToken;

        var payment = await _ctx.Set<Payment>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null || payment.Status != PaymentStatus.Confirmed) return;

        var tickets = await _ctx.Set<Ticket>().AsNoTracking()
            .Where(t => t.PaymentId == paymentId)
            .Include(t => t.Buyer)
            .Include(t => t.Tier)
            .Include(t => t.Price)
            .Include(t => t.Show).ThenInclude(s => s.Lounge)
            .OrderBy(t => t.Id) // "vé đầu" (đích nút Xem vé) phải cố định giữa các lần chạy lại job
            .ToListAsync(ct);

        var first = tickets.FirstOrDefault();
        // Không còn người mua (tài khoản đã xoá — BuyerId SET NULL) hoặc tài khoản không có email: không có ai để gửi.
        if (first?.Buyer is not { } buyer || string.IsNullOrWhiteSpace(buyer.Email)) return;

        var show = first.Show;
        var lines = tickets
            .GroupBy(t => new { t.TierId, t.PriceId })
            .Select(g => new TicketConfirmationLine(g.First().Tier.Name, g.First().Price.Name, g.Count(), g.First().Price.Price))
            .ToList();

        await _email.SendTicketConfirmationAsync(new TicketConfirmationEmail(
            ToEmail: buyer.Email,
            ToName: string.IsNullOrWhiteSpace(buyer.FullName) ? buyer.Email : buyer.FullName,
            Language: buyer.PreferredLanguage,
            OrderCode: payment.OrderId,
            ShowName: show.Name,
            Start: show.ScheduledStart,
            End: ShowSchedule.EffectiveEnd(show),
            LoungeName: show.Lounge.Name,
            LoungeAddress: show.Lounge.Address.FullAddress,
            Online: tickets.All(t => t.Tier.AccessType == AccessType.Livestream),
            Lines: lines,
            Total: payment.GrossAmount,
            // Một vé → mở thẳng vé đó; nhiều vé → vẫn mở vé đầu (trang vé có lối sang các vé cùng đơn qua Vé của tôi).
            TicketUrl: string.IsNullOrWhiteSpace(_business.TicketDetailUrl) ? null : $"{_business.TicketDetailUrl.TrimEnd('/')}/{first.Id}"), ct);
    }
}
