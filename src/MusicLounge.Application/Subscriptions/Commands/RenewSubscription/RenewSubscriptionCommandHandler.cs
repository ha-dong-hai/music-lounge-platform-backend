using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions.Commands.RenewSubscription;

// Payment-creation logic below intentionally mirrors SubscribeToPackageCommandHandler inline
// rather than composing it via ISender.Send — nesting a second ICommand (which also runs inside
// TransactionBehavior's ambient transaction) through MediatR here would try to open a 2nd
// transaction on the same connection, the exact bug already hit once in this codebase
// (ComplaintResolvedAction.TakeDownContent vs CancelLoungeShowCommandHandler) and fixed the same
// way there: inline the small amount of shared logic instead of composing commands.
internal sealed class RenewSubscriptionCommandHandler
    : IRequestHandler<RenewSubscriptionCommand, SubscriptionPaymentInitiationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IVnPayService _vnPay;
    private readonly BusinessSettings _settings;

    public RenewSubscriptionCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser,
        IVnPayService vnPay, IOptions<BusinessSettings> settings)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _settings = settings.Value;
    }

    public async Task<SubscriptionPaymentInitiationDto> Handle(
        RenewSubscriptionCommand request, CancellationToken ct)
    {
        var ownSubs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == _currentUser.UserId, ct);
        var lastSub = ownSubs.OrderByDescending(s => s.StartedAt).FirstOrDefault()
            ?? throw new DomainException(
                "Bạn chưa từng đăng ký gói subscription nào — vui lòng chọn một gói để đăng ký lần đầu.");

        var now = DateTimeOffset.UtcNow;
        var hasActiveSubscription = lastSub.Status == SubscriptionStatus.Active && lastSub.ExpiresAt > now;
        if (hasActiveSubscription)
            throw new ConflictException(
                "Bạn đã có gói đang hoạt động — đợi gói hiện tại hết hạn hoặc hủy trước khi gia hạn.");

        var package = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(lastSub.PackageId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPackage), lastSub.PackageId);

        // The package they were on may have been deactivated since — can't silently substitute a
        // different package they never agreed to, so fail with a clear pointer to the normal
        // "pick a package" flow instead of guessing a replacement.
        if (!package.IsActive)
            throw new DomainException(
                $"Gói \"{package.Name}\" hiện không còn mở đăng ký — vui lòng chọn gói khác để gia hạn.");

        var orderId = $"SUB-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        var payment = new Payment
        {
            OrderId = orderId,
            PayerId = _currentUser.UserId,
            GrossAmount = package.Price,
            Status = PaymentStatus.Pending,
            ReferenceType = "Subscription",
            ReferenceId = package.Id.ToString(),
            SubscriptionMaxTicketsPerEventSnapshot = package.MaxTicketsPerEvent,
            SubscriptionHasAiPosterSnapshot = package.HasAiPoster,
            SubscriptionMaxAiPostersPerMonthSnapshot = package.MaxAiPostersPerMonth,
            CreatedAt = now
        };
        _uow.Repository<Payment, int>().Add(payment);
        await _uow.SaveChangesAsync(ct);

        var paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: orderId,
            Amount: package.Price,
            OrderInfo: $"MusicLounge subscription renewal - {package.Name}",
            ReturnUrl: _settings.SubscriptionPaymentReturnUrl,
            IpAddress: request.ClientIpAddress));

        return new SubscriptionPaymentInitiationDto(payment.Id, orderId, package.Price, paymentUrl);
    }
}
