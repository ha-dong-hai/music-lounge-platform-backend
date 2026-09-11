using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Subscriptions.Commands.ChangeSubscriptionPackage;

// Tạo thanh toán inline như SubscribeToPackage / RenewSubscription — không lồng một ICommand khác qua MediatR
// (sẽ mở giao dịch thứ hai trên cùng kết nối; xem RenewSubscriptionCommandHandler).
internal sealed class ChangeSubscriptionPackageCommandHandler
    : IRequestHandler<ChangeSubscriptionPackageCommand, SubscriptionChangeInitiationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IVnPayService _vnPay;
    private readonly BusinessSettings _settings;

    public ChangeSubscriptionPackageCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IVnPayService vnPay, IOptions<BusinessSettings> settings)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _settings = settings.Value;
    }

    public async Task<SubscriptionChangeInitiationDto> Handle(ChangeSubscriptionPackageCommand request, CancellationToken ct)
    {
        var package = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(request.PackageId, ct)
            ?? throw new NotFoundException(nameof(SubscriptionPackage), request.PackageId);
        if (!package.IsActive)
            throw new DomainException("Gói này hiện không mở đăng ký.");

        // MLACP-376: cung chot voi SubscribeToPackage.
        await SubscriptionVenueGate.EnsureNotPenalizedAsync(_uow, _currentUser.UserId, ct);

        var now = DateTimeOffset.UtcNow;
        var current = (await _uow.Repository<OwnerSubscription, int>().FindAsync(
                s => s.OwnerId == _currentUser.UserId && s.Status == SubscriptionStatus.Active, ct))
            .FirstOrDefault(s => s.ExpiresAt > now)
            ?? throw new DomainException("Bạn chưa có gói đang hoạt động — hãy đăng ký một gói trước.");

        if (current.PackageId == package.Id)
            throw new DomainException("Đây là gói bạn đang dùng — dùng Gia hạn để cộng thêm thời gian.");

        await SubscriptionTerms.EnsureNoPendingChangeAsync(_uow, _currentUser.UserId, ct);

        var oldPackage = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(current.PackageId, ct);
        // MLACP-375: loai ngay duoc bu mien phi (tam khoa oan / khoa duoc go) khoi gia tri quy doi.
        var compensations = await _uow.Repository<VenuePenalty, int>().FindAsync(
            p => p.CompensatedSubscriptionId == current.Id, ct);
        var credit = SubscriptionTerms.RemainingValue(current, oldPackage?.Price ?? 0m, now, compensations);
        var cycleEnd = SubscriptionTerms.CycleEnd(package.BillingCycle, now);
        var extra = SubscriptionTerms.TimeWorth(credit, package.Price, cycleEnd - now);

        var orderId = SubscriptionTerms.NewOrderId(SubscriptionPurchase.Change, now);
        var payment = new Payment
        {
            OrderId = orderId,
            PayerId = _currentUser.UserId,
            GrossAmount = package.Price,
            Status = PaymentStatus.Pending,
            ReferenceType = SubscriptionPayments.ReferenceType,
            ReferenceId = package.Id.ToString(),
            SubscriptionMaxTicketsPerEventSnapshot = package.MaxTicketsPerEvent,
            SubscriptionHasAiPosterSnapshot = package.HasAiPoster,
            SubscriptionMaxAiPostersPerMonthSnapshot = package.MaxAiPostersPerMonth,
            SubscriptionMaxTourScenesSnapshot = package.MaxTourScenes,
            CreatedAt = now
        };
        _uow.Repository<Payment, int>().Add(payment);
        await _uow.SaveChangesAsync(ct);

        var paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: orderId,
            Amount: package.Price,
            OrderInfo: $"MusicLounge doi goi - {package.Name}",
            ReturnUrl: _settings.SubscriptionPaymentReturnUrl,
            IpAddress: request.ClientIpAddress));

        return new SubscriptionChangeInitiationDto(
            payment.Id, orderId, package.Price, paymentUrl,
            credit, Math.Round((decimal)extra.TotalDays, 1), cycleEnd + extra);
    }
}
