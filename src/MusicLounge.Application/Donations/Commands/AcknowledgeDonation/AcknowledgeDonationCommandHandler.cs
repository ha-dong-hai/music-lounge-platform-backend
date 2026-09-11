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

        donation.Status = DonationStatus.OwnerReceived;
        donation.OwnerAckAt = DateTimeOffset.UtcNow;
        _uow.Repository<Donation, int>().Update(donation);
        await _uow.SaveChangesAsync(ct);

        // MLACP-360: canh bao livestream khong con phat o day nua — no phat ngay luc VNPay xac nhan
        // (ProcessDonationPayment). Phat them o day la xuong mot donate hai lan tren song.

        return Unit.Value;
    }
}
