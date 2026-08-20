using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Livestreams.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;
using MusicLounge.Domain.Exceptions;

namespace MusicLounge.Application.Donations.Commands.AcknowledgeDonation;

internal sealed class AcknowledgeDonationCommandHandler : IRequestHandler<AcknowledgeDonationCommand, Unit>
{
    private readonly IUnitOfWork _uow;
    private readonly IDonationRepository _donationRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ILivestreamHubService _hubService;
    private readonly ILivestreamRepository _livestreamRepo;

    public AcknowledgeDonationCommandHandler(
        IUnitOfWork uow,
        IDonationRepository donationRepo,
        ICurrentUserService currentUser,
        ILivestreamHubService hubService,
        ILivestreamRepository livestreamRepo)
    {
        _uow = uow;
        _donationRepo = donationRepo;
        _currentUser = currentUser;
        _hubService = hubService;
        _livestreamRepo = livestreamRepo;
    }

    public async Task<Unit> Handle(AcknowledgeDonationCommand request, CancellationToken ct)
    {
        var donation = await _uow.Repository<Donation, int>().GetByIdAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        if (donation.Status == DonationStatus.Cancelled)
            throw new DomainException("Donation đã bị huỷ (thanh toán VNPay thất bại). Không thể xác nhận.");

        if (donation.Status != DonationStatus.PendingOwnerAck)
            throw new DomainException("Donation không ở trạng thái chờ xác nhận của Owner.");

        // Single JOIN query replaces 3 separate round-trips
        var ownership = await _donationRepo.GetOwnershipInfoAsync(request.DonationId, ct)
            ?? throw new NotFoundException(nameof(Donation), request.DonationId);

        if (ownership.OwnerId != _currentUser.UserId)
            throw new ForbiddenException("Chỉ Owner của venue này mới có thể xác nhận donation.");

        donation.Status = DonationStatus.OwnerReceived;
        donation.OwnerAckAt = DateTimeOffset.UtcNow;
        _uow.Repository<Donation, int>().Update(donation);
        await _uow.SaveChangesAsync(ct);

        // Broadcast donation alert to livestream if show is currently live
        var livestream = await _livestreamRepo.GetByShowIdAsync(ownership.LoungeShowId, ct);
        if (livestream?.Status == LivestreamStatus.Live)
        {
            var donorName = donation.IsAnonymous ? "Ẩn danh" : (donation.DisplayName ?? "Khán giả");
            await _hubService.BroadcastDonationAlertAsync(
                livestream.Id,
                new DonationAlertDto(donorName, donation.Gross, donation.IsMessagePublic ? donation.Message : null),
                ct);
        }

        return Unit.Value;
    }
}
