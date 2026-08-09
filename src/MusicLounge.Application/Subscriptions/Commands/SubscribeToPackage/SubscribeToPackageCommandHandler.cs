using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions.Commands.SubscribeToPackage;

internal sealed class SubscribeToPackageCommandHandler
    : IRequestHandler<SubscribeToPackageCommand, SubscriptionPaymentInitiationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IVnPayService _vnPay;
    private readonly BusinessSettings _settings;

    public SubscribeToPackageCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser,
        IVnPayService vnPay, IOptions<BusinessSettings> settings)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _settings = settings.Value;
    }

    public async Task<SubscriptionPaymentInitiationDto> Handle(
        SubscribeToPackageCommand request, CancellationToken ct)
    {
        var package = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(request.PackageId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPackage), request.PackageId);

        if (!package.IsActive)
            throw new DomainException("Gói này hiện không mở đăng ký.");

        var now = DateTimeOffset.UtcNow;
        var activeStatusSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == _currentUser.UserId && s.Status == SubscriptionStatus.Active, ct);
        var hasActiveSubscription = activeStatusSubs.Any(s => s.ExpiresAt > now);

        if (hasActiveSubscription)
            throw new ConflictException(
                "Bạn đã có gói đang hoạt động — đợi gói hiện tại hết hạn hoặc hủy trước khi đăng ký gói mới.");

        var orderId = $"SUB-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        var payment = new Payment
        {
            OrderId = orderId,
            PayerId = _currentUser.UserId,
            GrossAmount = package.Price,
            Status = PaymentStatus.Pending,
            ReferenceType = "Subscription",
            ReferenceId = package.Id.ToString(),
            // Snapshot now, not just GrossAmount — UpdateSubscriptionPackageCommandHandler only
            // locks these fields once a real Active OwnerSubscription exists, which doesn't happen
            // until VNPay confirms. Without this, an admin edit landing in that pending window
            // would silently change what the owner receives for what they already paid for.
            SubscriptionMaxTicketsPerEventSnapshot = package.MaxTicketsPerEvent,
            SubscriptionHasAiPosterSnapshot = package.HasAiPoster,
            CreatedAt = now
        };
        _uow.Repository<Payment, int>().Add(payment);
        await _uow.SaveChangesAsync(ct);

        var paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: orderId,
            Amount: package.Price,
            OrderInfo: $"MusicLounge subscription - {package.Name}",
            ReturnUrl: _settings.SubscriptionPaymentReturnUrl,
            IpAddress: request.ClientIpAddress));

        return new SubscriptionPaymentInitiationDto(payment.Id, orderId, package.Price, paymentUrl);
    }
}
