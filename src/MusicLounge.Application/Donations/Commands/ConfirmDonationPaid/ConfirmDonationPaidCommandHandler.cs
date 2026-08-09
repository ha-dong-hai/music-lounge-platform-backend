using MediatR;
using Microsoft.Extensions.Logging;
using MusicLounge.Application.Common;
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
    private readonly ISystemConfigService _config;
    private readonly ILogger<ConfirmDonationPaidCommandHandler> _logger;

    public ConfirmDonationPaidCommandHandler(
        IUnitOfWork uow,
        IDonationRepository donationRepo,
        ICurrentUserService currentUser,
        ILedgerService ledger,
        ISystemConfigService config,
        ILogger<ConfirmDonationPaidCommandHandler> logger)
    {
        _uow = uow;
        _donationRepo = donationRepo;
        _currentUser = currentUser;
        _ledger = ledger;
        _config = config;
        _logger = logger;
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

        // Snapshot which bank account this payout actually went to — Donation.BankAccountId was
        // previously defined and commented "D12: FK snapshot" but never assigned anywhere, leaving
        // every confirmed payout with no system-of-record for its destination. Fail closed rather
        // than confirm a payment silently going nowhere on record: the Owner is asserting money
        // already moved (PaymentRef/evidence prove it), so demand the performer profile actually
        // has a payout account registered before that assertion is accepted.
        var performerAccounts = await _uow.Repository<BankAccount, int>().FindAsync(
            a => a.OwnerType == BankAccountOwnerType.Performer && a.OwnerId == ownership.PerformerId && a.IsDefault, ct);
        var bankAccountId = performerAccounts.FirstOrDefault()?.Id
            ?? throw new DomainException(
                "Nghệ sĩ chưa đăng ký tài khoản ngân hàng mặc định — không thể xác nhận đã thanh toán cho tới khi có tài khoản để ghi nhận.");

        donation.Status = DonationStatus.PerformerPaid;
        donation.OwnerPaidAt = DateTimeOffset.UtcNow;
        donation.PaymentRef = request.PaymentRef;
        donation.PaymentEvidenceUrl = request.PaymentEvidenceUrl;
        donation.BankAccountId = bankAccountId;

        _uow.Repository<Donation, int>().Update(donation);

        // Chặng 2 (§6.5): owner forwards a configurable share of the ORIGINAL gross to the
        // performer (default 88% — system_config, tunable by Admin without a deploy, §6.7), keeping
        // the rest of their chặng-1 net for holding/administering the donation. Uses donation.Net
        // (chặng 1's committed figure) rather than re-deriving it from today's commission/tax rates
        // — same snapshot-at-commitment-point reasoning as WriteTicketLedgerHandler.
        var performerShareRate = await _config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        var split = PaymentFeeCalculator.SplitDonationPayout(donation.Gross, donation.Net, performerShareRate);
        if (split.OwnerRetained < 0)
            throw new DomainException(
                $"Cấu hình donation_performer_share_rate ({performerShareRate:P0}) vượt quá phần owner thực nhận — kiểm tra lại system_config.");

        await _ledger.WriteJournalAsync(
            Guid.NewGuid().ToString("N"),
            LedgerReferenceTypes.Donation,
            donation.Id.ToString(),
            paymentId: null,
            new LedgerLine[]
            {
                new(AccountType.User, ownership.OwnerId, split.PerformerAmount, IsDebit: true,
                    Description: $"Donate #{donation.Id} — chặng 2, trả nghệ sĩ ({performerShareRate:P0} gross)"),
                new(AccountType.Performer, ownership.PerformerId, split.PerformerAmount, IsDebit: false,
                    Description: $"Donate #{donation.Id} — nhận từ chủ phòng trà")
            }, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Donation payout to performer confirmed: DonationId={DonationId} PerformerAmount={PerformerAmount} OwnerRetained={OwnerRetained} OwnerId={OwnerId} PerformerId={PerformerId} PaymentRef={PaymentRef}",
            donation.Id, split.PerformerAmount, split.OwnerRetained, ownership.OwnerId, ownership.PerformerId, request.PaymentRef);

        return Unit.Value;
    }
}
