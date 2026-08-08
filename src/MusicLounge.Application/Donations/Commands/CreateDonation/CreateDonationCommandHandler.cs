using MediatR;
using Microsoft.Extensions.Options;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Settings;
using MusicLounge.Application.Donations.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Donations.Commands.CreateDonation;

internal sealed class CreateDonationCommandHandler
    : IRequestHandler<CreateDonationCommand, DonationInitiationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IVnPayService _vnPay;
    private readonly ISystemConfigService _config;
    private readonly BusinessSettings _settings;

    public CreateDonationCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IVnPayService vnPay,
        ISystemConfigService config,
        IOptions<BusinessSettings> settings)
    {
        _uow = uow;
        _currentUser = currentUser;
        _vnPay = vnPay;
        _config = config;
        _settings = settings.Value;
    }

    public async Task<DonationInitiationDto> Handle(CreateDonationCommand request, CancellationToken ct)
    {
        var performance = await _uow.Repository<Performance, int>().GetByIdAsync(request.PerformanceId, ct)
            ?? throw new NotFoundException(nameof(Performance), request.PerformanceId);

        var show = await _uow.Repository<LoungeShow, int>().GetByIdAsync(performance.LoungeShowId, ct)
            ?? throw new NotFoundException(nameof(LoungeShow), performance.LoungeShowId);

        if (show.Status != LoungeShowStatus.Ongoing)
            throw new DomainException("Chỉ có thể donate khi show đang diễn ra.");

        if (!performance.AcceptsDonation)
            throw new DomainException("Nghệ sĩ này không nhận donate.");


        // Load donor display name from profile (only if not anonymous)
        string? displayName = null;
        if (!request.IsAnonymous)
        {
            var donor = await _uow.Repository<User, int>().GetByIdAsync(_currentUser.UserId, ct);
            displayName = donor?.FullName;
        }

        // Gross = amount Audience pays. Net = amount Performer receives (after platform commission + tax)
        var commissionRate = await _config.GetDecimalAsync(ConfigKeys.PlatformCommissionRate, 0.05m, ct);
        var taxRate = await _config.GetDecimalAsync(ConfigKeys.TaxRate, 0.05m, ct);
        var platformFee = Math.Round(request.Amount * commissionRate, 2);
        var tax = Math.Round(request.Amount * taxRate, 2);
        var net = request.Amount - platformFee - tax;

        var orderId = $"DON-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];

        var donation = new Donation
        {
            DonorUserId = _currentUser.IsAuthenticated ? _currentUser.UserId : null,
            PerformanceId = request.PerformanceId,
            Gross = request.Amount,
            Net = net,
            Status = DonationStatus.PendingPayment,
            IsAnonymous = request.IsAnonymous,
            DisplayName = displayName,
            Message = request.Message,
            IsMessagePublic = request.IsMessagePublic,
            IsAmountPublic = true,
            GatewayRef = orderId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _uow.Repository<Donation, int>().Add(donation);
        await _uow.SaveChangesAsync(ct);

        var paymentUrl = _vnPay.CreatePaymentUrl(new VnPayPaymentRequest(
            OrderId: orderId,
            Amount: request.Amount,
            OrderInfo: $"Donate cho nghệ sĩ - performance #{request.PerformanceId}",
            ReturnUrl: _settings.DonationPaymentReturnUrl,
            IpAddress: request.ClientIpAddress));

        return new DonationInitiationDto(donation.Id, orderId, request.Amount, paymentUrl);
    }
}
