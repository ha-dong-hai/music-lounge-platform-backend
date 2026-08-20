using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.CancelTicket;

internal sealed class CancelTicketCommandHandler : IRequestHandler<CancelTicketCommand, int>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;

    public CancelTicketCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _lock = @lock;
    }

    public async Task<int> Handle(CancelTicketCommand request, CancellationToken ct)
    {
        // Double-click "Hủy vé" is the failure this guards against: without a lock, both requests
        // can read Status == Confirmed before either commits, and both insert a RefundRequest for
        // the same PaymentId.
        await using var _ = await _lock.AcquireAsync($"cancel-ticket:{request.TicketId}", ct);

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        var ticket = await ticketRepo.GetByIdAsync(request.TicketId, ct)
            ?? throw new NotFoundException(nameof(Ticket), request.TicketId);

        if (ticket.BuyerId != _currentUser.UserId)
            throw new ForbiddenException("Vé này không thuộc về bạn.");

        // Ve Pending = don thanh toan chua hoan tat (chua tung tra tien that) — cho phep buyer tu
        // huy bo NGAY, khong can cho het 15-30 phut de job nen tu dong huy. Khac hoan toan ve
        // Confirmed (da tra tien that, huy phai qua RefundRequest o duoi) nen tach nhanh rieng,
        // khong ap dung chinh sach huy/deadline cua show (nhung dieu do chi danh cho ve da mua that).
        if (ticket.Status == TicketStatus.Pending)
        {
            ticket.Status = TicketStatus.Cancelled;
            ticketRepo.Update(ticket);

            if (ticket.PaymentId.HasValue)
            {
                var pendingPayment = await _uow.Repository<Payment, int>().GetByIdAsync(ticket.PaymentId.Value, ct);
                if (pendingPayment is not null && pendingPayment.Status == PaymentStatus.Pending)
                {
                    pendingPayment.Status = PaymentStatus.Failed;
                    pendingPayment.UpdatedAt = DateTimeOffset.UtcNow;
                    _uow.Repository<Payment, int>().Update(pendingPayment);
                }
            }

            await _uow.SaveChangesAsync(ct);
            return 0;
        }

        if (ticket.Status != TicketStatus.Confirmed)
            throw new DomainException("Chỉ có thể hủy vé đã xác nhận hoặc đang chờ thanh toán.");

        if (ticket.PendingTransferToUserId is not null)
            throw new DomainException("Vé đang trong quá trình chuyển nhượng, không thể hủy lúc này.");

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(ticket.ShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), ticket.ShowId);

        if (!show.CancellationAllowed)
            throw new DomainException("Event này không cho phép hủy vé.");

        if (show.CancellationDeadlineHours.HasValue &&
            DateTimeOffset.UtcNow > show.ScheduledStart.AddHours(-show.CancellationDeadlineHours.Value))
            throw new DomainException(
                $"Đã quá hạn hủy vé — event yêu cầu hủy trước {show.CancellationDeadlineHours.Value} giờ so với giờ diễn. Vui lòng liên hệ phòng trà nếu cần hỗ trợ thêm.");

        if (ticket.PaymentId is null)
            throw new DomainException("Vé này không có giao dịch thanh toán hợp lệ để hoàn tiền.");

        var price = await _uow.Repository<TicketPrice, int>().GetByIdAsync(ticket.PriceId, ct)
            ?? throw new NotFoundException(nameof(TicketPrice), ticket.PriceId);

        ticket.Status = TicketStatus.Cancelled;
        ticketRepo.Update(ticket);

        var refundPercentage = show.RefundPercentage ?? 100m;
        var refundRequest = new RefundRequest
        {
            PaymentId = ticket.PaymentId.Value,
            RequestedBy = _currentUser.UserId,
            Reason = "Audience yêu cầu hủy vé",
            AmountRequested = Math.Round(price.Price * refundPercentage / 100m, 2),
            RefundPercentage = refundPercentage,
            Status = RefundRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _uow.Repository<RefundRequest, int>().Add(refundRequest);
        await _uow.SaveChangesAsync(ct);

        return refundRequest.Id;
    }
}
