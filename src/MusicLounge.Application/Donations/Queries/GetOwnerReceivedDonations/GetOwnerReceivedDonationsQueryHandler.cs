using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetOwnerReceivedDonations;

internal sealed class GetOwnerReceivedDonationsQueryHandler
    : IRequestHandler<GetOwnerReceivedDonationsQuery, PaginatedResult<PendingDonationDto>>
{
    private readonly IDonationRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;

    public GetOwnerReceivedDonationsQueryHandler(
        IDonationRepository repo, IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config)
    {
        _repo = repo;
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<PaginatedResult<PendingDonationDto>> Handle(
        GetOwnerReceivedDonationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var performerShareRate = await _config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        var result = await _repo.GetOwnerReceivedAwaitingPayoutAsync(_currentUser.UserId, performerShareRate, page, size, ct);

        // MLACP-362: han chuyen cho nghe si, cung moc voi nhac nho va canh cao cua DonationOverdueCheckJob.
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(_config, ct);
        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(
            _uow, result.Items.Select(i => i.Id).ToList(), ct);
        var items = result.Items.Select(i =>
        {
            var receivedAt = DonationPayoutDeadline.ReceivedAt(i.Id, i.PaymentConfirmedAt, null, releaseTimes);
            return i with { PayoutReceivedAt = receivedAt, PayoutDueAt = DonationPayoutDeadline.DueAt(receivedAt, holdDays) };
        }).ToList();
        return new PaginatedResult<PendingDonationDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}
