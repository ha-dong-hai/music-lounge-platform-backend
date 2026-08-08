using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Donations.Commands.ConfirmDonationPaid;

internal sealed class ConfirmDonationPaidCommandHandler : IRequestHandler<ConfirmDonationPaidCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IDonationRepository _donationRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILedgerService _ledger;

    public ConfirmDonationPaidCommandHandler(
        IUnitOfWork uow,
        IDonationRepository donationRepo,
        ICurrentUserService currentUser,
        ILedgerService ledger)
    {
        _uow = uow;
        _donationRepo = donationRepo;
        _currentUser = currentUser;
        _ledger = ledger;
    }

    public async Task<Unit> Handle(ConfirmDonationPaidCommand request, CancellationToken ct)
    {
        var donation = await _uow.Repository<Donation, int>().GetByIdAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        if (donation.Status != DonationStatus.OwnerReceived)
            throw new DomainException("Owner chỉ có thể xác nhận đã trả sau khi đã xác nhận nhận tiền.");

        // Single JOIN query replaces 3 separate round-trips
        var ownership = await _donationRepo.GetOwnershipInfoAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        if (ownership.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Chỉ Owner của venue này mới có thể xác nhận thanh toán cho nghệ sĩ.");

        donation.Status = DonationStatus.PerformerPaid;
        donation.OwnerPaidAt = DateTimeOffset.UtcNow;
        donation.PaymentRef = request.PaymentRef;
        donation.PaymentEvidenceUrl = request.PaymentEvidenceUrl;

        _uow.Repository<Donation, int>().Update(donation);

        // Chặng 2 (§6.5) — money leaves the owner's held balance and is recorded as delivered to
        // the performer. NOTE: docs describe this tranche as 88% of gross (owner keeps a further
        // 2% on top of the platform's 5%+5%), but ConfirmDonationPaidCommand has no amount field
        // — Owner can only attest "paid" with a reference/evidence URL, no partial amount. This
        // records the full donation.Net (the only figure the system actually tracks) as
        // transferred; if the 88%/2% split is real product intent, ConfirmDonationPaidCommand
        // needs an amount field and this needs revisiting — flagged, not silently assumed.
        await _ledger.WriteJournalAsync(
            Guid.NewGuid().ToString("N"),
            LedgerReferenceTypes.Donation,
            donation.Id.ToString(),
            paymentId: null,
            new LedgerLine[]
            {
                new(AccountType.User, ownership.OwnerId, donation.Net, IsDebit: true,
                    Description: $"Donate #{donation.Id} — chặng 2, trả nghệ sĩ"),
                new(AccountType.Performer, ownership.PerformerId, donation.Net, IsDebit: false,
                    Description: $"Donate #{donation.Id} — nhận từ chủ phòng trà")
            }, ct);

        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
