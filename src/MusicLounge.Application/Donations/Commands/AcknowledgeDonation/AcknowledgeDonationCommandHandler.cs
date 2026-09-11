using MediatR;
using MusicLounge.Application.Common.Constants;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Donations.Commands.AcknowledgeDonation;

internal sealed class AcknowledgeDonationCommandHandler : IRequestHandler<AcknowledgeDonationCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IDonationRepository _donationRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IAsyncKeyedLock _lock;

    public AcknowledgeDonationCommandHandler(
        IUnitOfWork uow,
        IDonationRepository donationRepo,
        ICurrentUserService currentUser,
        IAsyncKeyedLock @lock)
    {
        _uow = uow;
        _donationRepo = donationRepo;
        _currentUser = currentUser;
        _lock = @lock;
    }

    public async Task<Unit> Handle(AcknowledgeDonationCommand request, CancellationToken ct)
    {
        // Same key namespace as ConfirmDonationPaidCommandHandler — serializing against chặng-2
        // avoids any overlap between the two status writes.
        await using var _ = await _lock.AcquireAsync($"donation:{request.DonationId}", ct);

        var donation = await _uow.Repository<Donation, int>().GetByIdAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        if (donation.Status == DonationStatus.Cancelled)
            throw new DomainException("Donation đã bị huỷ (thanh toán VNPay thất bại). Không thể xác nhận.");

        if (donation.Status != DonationStatus.PendingOwnerAck)
            throw new DomainException("Donation không ở trạng thái chờ xác nhận của Owner.");

        // Single JOIN query replaces 3 separate round-trips
        var ownership = await _donationRepo.GetOwnershipInfoAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        if (ownership.OwnerId != _currentUser.UserId && _currentUser.Role != Roles.Admin)
            throw new ForbiddenException("Chỉ Owner của venue này mới có thể xác nhận donation.");

        // MLACP-361: "da nhan" phai nghia la tien da ve tai khoan phong tra. Kiem SAU quyen han, de nguoi
        // ngoai van nhan 403 chu khong biet duoc tinh trang chi tra cua mot donate khong phai cua ho.
        // Donate co tu truoc MLACP-361 khong co khoan quyet toan — giu nguyen hanh vi cu.
        var payout = await DonationPayouts.StateAsync(_uow, donation.Id, ct);
        if (payout.HasPayout && payout.ReleasedAt is null)
            throw new DomainException(payout.HasBankAccount
                ? "Nền tảng chưa chuyển khoản donate này cho phòng trà — sẽ chuyển ở lần giải ngân tới. " +
                  "Chỉ xác nhận đã nhận sau khi tiền đã về tài khoản."
                : "Phòng trà chưa đăng ký tài khoản ngân hàng mặc định nên nền tảng chưa chuyển được khoản " +
                  "donate này. Hãy thêm tài khoản; khoản này sẽ được chuyển ở lần giải ngân tới.");

        donation.Status = DonationStatus.OwnerReceived;
        donation.OwnerAckAt = DateTimeOffset.UtcNow;
        _uow.Repository<Donation, int>().Update(donation);
        await _uow.SaveChangesAsync(ct);

        // MLACP-360: canh bao livestream khong con phat o day nua — no phat ngay luc VNPay xac nhan
        // (ProcessDonationPayment). Phat them o day la xuong mot donate hai lan tren song.

        return Unit.Value;
    }
}
