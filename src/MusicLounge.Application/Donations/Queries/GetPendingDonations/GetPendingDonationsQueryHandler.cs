using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Common.Interfaces.Repositories;
using MusicLounge.Application.Common.Models;
using MusicLounge.Application.Donations.DTOs;

namespace MusicLounge.Application.Donations.Queries.GetPendingDonations;

internal sealed class GetPendingDonationsQueryHandler
    : IRequestHandler<GetPendingDonationsQuery, PaginatedResult<PendingDonationDto>>
{
    private readonly IDonationRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemConfigService _config;

    public GetPendingDonationsQueryHandler(
        IDonationRepository repo, IUnitOfWork uow, ICurrentUserService currentUser, ISystemConfigService config)
    {
        _repo = repo;
        _uow = uow;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<PaginatedResult<PendingDonationDto>> Handle(
        GetPendingDonationsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 50);
        var performerShareRate = await _config.GetDecimalAsync(ConfigKeys.DonationPerformerShareRate, 0.88m, ct);
        var result = await _repo.GetPendingForOwnerAsync(_currentUser.UserId, performerShareRate, page, size, ct);

        // MLACP-362: han tu xac nhan = han chuyen cho nghe si = luc phong tra that su nhan tien + hold.
        // Nen tang chua chuyen thi chua co han nao (null) — khong phai "con 24 gio" nhu truoc day.
        var holdDays = await DonationPayoutDeadline.HoldDaysAsync(_config, ct);
        var releaseTimes = await DonationPayoutDeadline.PayoutReleaseTimesAsync(
            _uow, result.Items.Select(i => i.Id).ToList(), ct);
        var items = result.Items.Select(i =>
        {
            var receivedAt = DonationPayoutDeadline.ReceivedAt(i.Id, i.PaymentConfirmedAt, null, releaseTimes);
            var dueAt = DonationPayoutDeadline.DueAt(receivedAt, holdDays);
            return i with { AutoConfirmDeadline = dueAt, PayoutReceivedAt = receivedAt, PayoutDueAt = dueAt };
        }).ToList();
        return new PaginatedResult<PendingDonationDto>(items, result.Page, result.PageSize, result.TotalCount);
    }
}
