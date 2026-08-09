using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Tickets.Events;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Tickets.Commands.ProcessVnPayCallback;

internal sealed class ProcessVnPayCallbackCommandHandler
    : IRequestHandler<ProcessVnPayCallbackCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly IVnPayService _vnPay;
    private readonly ILoungeShowRepository _showRepo;
    private readonly ILivestreamRepository _livestreamRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly IPublisher _publisher;
    private readonly IAsyncKeyedLock _lock;
    private readonly ILogger<ProcessVnPayCallbackCommandHandler> _logger;

    public ProcessVnPayCallbackCommandHandler(
        IUnitOfWork uow,
        IVnPayService vnPay,
        ILoungeShowRepository showRepo,
        ILivestreamRepository livestreamRepo,
        ITicketRepository ticketRepo,
        IPublisher publisher,
        IAsyncKeyedLock @lock,
        ILogger<ProcessVnPayCallbackCommandHandler> logger)
    {
        _uow = uow;
        _vnPay = vnPay;
        _showRepo = showRepo;
        _livestreamRepo = livestreamRepo;
        _ticketRepo = ticketRepo;
        _publisher = publisher;
        _lock = @lock;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessVnPayCallbackCommand request, CancellationToken ct)
    {
        var result = _vnPay.VerifyCallback(request.QueryParams);
        request.QueryParams.TryGetValue("vnp_TxnRef", out var txnRefForLogging);

        // Reject tampered/forged callbacks immediately — do NOT modify any data. Logged distinctly
        // from every other rejection below (previously this whole handler had zero logging — a
        // forged signature, an already-processed replay, and a genuinely-confirmed payment were all
        // indistinguishable from log output alone for this codebase's single highest-stakes inbound
        // integration).
        if (!result.IsSignatureValid)
        {
            _logger.LogWarning(
                "VNPay ticket callback rejected — invalid signature: TxnRef={TxnRef} at {At}",
                txnRefForLogging, DateTimeOffset.UtcNow);
            return false;
        }

        var txnRef = txnRefForLogging;

        // Same VNPay retry-storm hazard as donations/subscriptions — without this lock, 2
        // near-simultaneous callbacks both read Status==Pending before either commits, both
        // confirm the tickets and both insert PhysicalTicketDetail/LivestreamTicketDetail rows.
        await using var _ = await _lock.AcquireAsync($"vnpay-ticket:{txnRef}", ct);

        var paymentRepo = _uow.Repository<Payment, int>();
        var payments = await paymentRepo.FindAsync(p => p.OrderId == txnRef, ct);

        var payment = payments.FirstOrDefault();
        if (payment is null)
        {
            _logger.LogWarning(
                "VNPay ticket callback rejected — no Payment found for TxnRef={TxnRef} at {At}",
                txnRef, DateTimeOffset.UtcNow);
            return false;
        }

        // Idempotency: VNPay có thể gọi lại nhiều lần. Nếu đã xử lý rồi thì bỏ qua
        // để tránh overwrite QrCode và tạo trùng LivestreamTicketDetail.
        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogInformation(
                "VNPay ticket callback replay — PaymentId={PaymentId} TxnRef={TxnRef} already {Status} at {At}",
                payment.Id, txnRef, payment.Status, DateTimeOffset.UtcNow);
            return payment.Status == PaymentStatus.Confirmed;
        }

        // Signature only proves VNPay sent this callback, not that it's for the amount we asked
        // for — fail closed on mismatch instead of confirming tickets for an unexpected amount.
        if (result.IsSuccess && result.Amount != payment.GrossAmount)
        {
            _logger.LogWarning(
                "VNPay ticket callback rejected — amount mismatch: PaymentId={PaymentId} TxnRef={TxnRef} Expected={Expected} Received={Received} at {At}",
                payment.Id, txnRef, payment.GrossAmount, result.Amount, DateTimeOffset.UtcNow);
            return false;
        }

        var ticketRepo = _uow.Repository<Ticket, Guid>();
        var tickets = await ticketRepo.FindAsync(t => t.PaymentId == payment.Id, ct);

        if (result.IsSuccess)
        {
            payment.Status = PaymentStatus.Confirmed;
            payment.TransactionId = result.TransactionId;
            payment.VnPayResponseCode = result.ResponseCode;
            payment.PaidAt = DateTimeOffset.UtcNow;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            paymentRepo.Update(payment);

            foreach (var ticket in tickets)
            {
                ticket.Status = TicketStatus.Confirmed;
                ticket.QrCode = Guid.NewGuid().ToString("N");
                ticketRepo.Update(ticket);
            }

            // For tier-specific ticket details
            var firstTicket = tickets.FirstOrDefault();
            if (firstTicket is not null)
            {
                var tier = await _uow.Repository<TicketTier, int>().GetByIdAsync(firstTicket.TierId, ct);
                if (tier?.AccessType == AccessType.Livestream)
                {
                    var livestream = await _livestreamRepo.GetByShowIdAsync(firstTicket.ShowId, ct);
                    if (livestream is not null)
                    {
                        foreach (var ticket in tickets)
                            _livestreamRepo.AddTicketDetail(new LivestreamTicketDetail
                            {
                                TicketId = ticket.Id,
                                LivestreamId = livestream.Id,
                                AccessToken = Guid.NewGuid().ToString("N")
                            });
                    }
                }
                else if (tier?.AccessType == AccessType.Physical)
                {
                    foreach (var ticket in tickets)
                        _ticketRepo.AddPhysicalDetail(new PhysicalTicketDetail { TicketId = ticket.Id });
                }
            }

            await _uow.SaveChangesAsync(ct);

            var ownerId = firstTicket is not null
                ? await _showRepo.GetLoungeOwnerIdAsync(firstTicket.ShowId, ct) ?? 0
                : 0;

            await _publisher.Publish(new TicketPaymentConfirmed(
                PaymentId: payment.Id,
                UserId: firstTicket?.BuyerId ?? 0,
                OwnerId: ownerId,
                TicketIds: tickets.Select(t => t.Id).ToArray(),
                LivestreamId: null), ct);

            _logger.LogInformation(
                "VNPay ticket callback confirmed: PaymentId={PaymentId} TxnRef={TxnRef} TicketCount={TicketCount} at {At}",
                payment.Id, txnRef, tickets.Count, DateTimeOffset.UtcNow);
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.VnPayResponseCode = result.ResponseCode;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            paymentRepo.Update(payment);

            foreach (var ticket in tickets)
            {
                ticket.Status = TicketStatus.Cancelled;
                ticketRepo.Update(ticket);
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "VNPay ticket callback failed at gateway: PaymentId={PaymentId} TxnRef={TxnRef} ResponseCode={ResponseCode} at {At}",
                payment.Id, txnRef, result.ResponseCode, DateTimeOffset.UtcNow);
        }

        return result.IsSuccess;
    }
}
