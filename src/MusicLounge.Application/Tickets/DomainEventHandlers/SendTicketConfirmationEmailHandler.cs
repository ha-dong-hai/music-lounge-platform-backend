using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Tickets.Events;

namespace MusicLounge.Application.Tickets.DomainEventHandlers;

/// <summary>
/// MLACP-635. Thanh toán vé online thành công → xếp job gửi thư xác nhận vé (SendTicketConfirmationEmailJob).
/// Chạy cạnh SendFcmConfirmHandler (thông báo trong ứng dụng + push) chứ không thay nó: thư là bản người mua giữ được ngoài
/// ứng dụng.
/// </summary>
internal sealed class SendTicketConfirmationEmailHandler : INotificationHandler<TicketPaymentConfirmed>
{
    private readonly IBackgroundJobService _jobs;
    private readonly ILogger<SendTicketConfirmationEmailHandler> _logger;

    public SendTicketConfirmationEmailHandler(IBackgroundJobService jobs, ILogger<SendTicketConfirmationEmailHandler> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    public Task Handle(TicketPaymentConfirmed notification, CancellationToken ct)
    {
        // Vé tại quầy phát sự kiện này với UserId rỗng (khách không có tài khoản, không có email) — không có ai để gửi.
        if (notification.UserId == Guid.Empty) return Task.CompletedTask;

        // Sự kiện được phát NGAY TRONG luồng xác nhận thanh toán (callback VNPay). Một bức thư không được làm hỏng luồng đó:
        // tiền đã ghi xong; xếp job lỗi (vd kho Hangfire tạm mất kết nối) thì chỉ ghi log, người mua vẫn có vé.
        try
        {
            _jobs.EnqueueTicketConfirmationEmail(notification.PaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không xếp được thư xác nhận vé cho thanh toán {PaymentId}", notification.PaymentId);
        }
        return Task.CompletedTask;
    }
}
