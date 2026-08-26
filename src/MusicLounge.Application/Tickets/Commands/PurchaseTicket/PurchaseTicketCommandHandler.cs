using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Tickets.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Tickets.Commands.PurchaseTicket;

internal sealed class PurchaseTicketCommandHandler
    : IRequestHandler<PurchaseTicketCommand, PaymentInitiationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IVnPayService _vnPay;
    private readonly BusinessSettings _settings;
    private readonly IAsyncKeyedLock _lock;

    public PurchaseTicketCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser,
        IVnPayService vnPay, IOptions<BusinessSettings> settings, IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _settings = settings.Value;
        _lock = @lock;
    }

    public async Task<PaymentInitiationDto> Handle(
        PurchaseTicketCommand request, CancellationToken ct)
    {
        // Double-click / client retry-after-timeout on the same hold is the failure this guards
        // against: without a lock, both requests can read IsReleased == false before either
        // commits, and both create a Payment + a batch of Pending tickets from the SAME hold.
        await using var _ = await _lock.AcquireAsync($"purchase-hold:{request.HoldId}", ct);

        var holdRepo = _uow.Repository<TicketHold, int>();
        var hold = await holdRepo.GetByIdAsync(request.HoldId, ct)
            ?? throw new NotFoundException(nameof(TicketHold), request.HoldId);

        if (hold.UserId != _currentUser.UserId)
            throw new ForbiddenException("Vé giữ chỗ này không thuộc về bạn.");

        if (hold.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new DomainException("Vé giữ chỗ đã hết hạn. Vui lòng thực hiện lại.");

        // Was never checked — a hold already spent by a prior successful purchase would silently
        // pass every check above and mint a second Payment+Ticket batch for the same seats.
        if (hold.IsReleased)
            throw new ConflictException("Vé giữ chỗ này đã được dùng để mua vé rồi.");

        var priceRepo = _uow.Repository<TicketPrice, int>();
        var price = await priceRepo.GetByIdAsync(hold.PriceId, ct)
            ?? throw new NotFoundException(nameof(TicketPrice), hold.PriceId);

        var tierRepo = _uow.Repository<TicketTier, int>();
        var tier = await tierRepo.GetByIdAsync(price.TierId, ct)
            ?? throw new NotFoundException(nameof(TicketTier), price.TierId);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(tier.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), tier.LoungeShowId);

        if (show.Status is not LoungeShowStatus.Published and not LoungeShowStatus.Ongoing)
            throw new DomainException(
                $"Show này không còn mở bán vé (trạng thái hiện tại: '{show.Status}'). Vui lòng quay lại trang show để kiểm tra tình trạng bán vé mới nhất.");

        var totalAmount = price.Price * hold.Quantity;
        var orderId = $"ML-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        var payment = new Payment
        {
            OrderId = orderId,
            GrossAmount = totalAmount,
            Status = PaymentStatus.Pending,
            ReferenceType = "TicketHold",
            ReferenceId = hold.Id.ToString(),
            // A hold can back at most 1 Payment — final DB-level backstop behind the IsReleased
            // check and the lock above (PaymentConfiguration already has a filtered unique index
            // on this column; it was just never populated for ticket purchases).
            IdempotencyKey = $"hold:{hold.Id}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _uow.Repository<Payment, int>().Add(payment);
        await _uow.SaveChangesAsync(ct);   // get payment.Id

        var tickets = Enumerable.Range(0, hold.Quantity).Select(_ => new Ticket
        {
            Id = Guid.NewGuid(),
            BuyerId = _currentUser.UserId,
            PriceId = price.Id,
            TierId = tier.Id,
            ShowId = tier.LoungeShowId,
            PaymentId = payment.Id,
            Status = TicketStatus.Pending,
            PurchaseChannel = PurchaseChannel.Online,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        _uow.Repository<Ticket, Guid>().AddRange(tickets);

        // Release the hold now that pending tickets exist — prevents double-counting
        // in quota checks (confirmed/pending tickets + active holds both count against quota).
        hold.IsReleased = true;
        hold.ReleasedAt = DateTimeOffset.UtcNow;
        _uow.Repository<TicketHold, int>().Update(hold);

        await _uow.SaveChangesAsync(ct);

        var paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: orderId,
            Amount: totalAmount,
            OrderInfo: $"MusicLounge ticket purchase - {hold.Quantity} ticket(s)",
            ReturnUrl: _settings.TicketPaymentReturnUrl,
            IpAddress: request.ClientIpAddress));

        return new PaymentInitiationDto(
            payment.Id,
            orderId,
            totalAmount,
            paymentUrl,
            tickets.Select(t => t.Id).ToArray());
    }
}
