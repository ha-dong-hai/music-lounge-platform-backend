using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Users.DTOs;
using MusicLounge.Domain.Entities;
using MusicLounge.Domain.Enums;

namespace MusicLounge.Application.Users.Queries.GetMyEarnings;

internal sealed class GetMyEarningsQueryHandler : IRequestHandler<GetMyEarningsQuery, EarningsSummaryDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMyEarningsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<EarningsSummaryDto> Handle(GetMyEarningsQuery request, CancellationToken ct)
    {
        var settlements = await _uow.Repository<Settlement, int>()
            .FindAsync(s => s.OwnerId == _currentUser.UserId, ct);

        // PendingReview (D16 — actual/scheduled show duration ratio missed the completion
        // threshold, parked for Admin to decide) is still money owed to the Owner, just not yet
        // released. Counting only Scheduled here silently dropped PendingReview settlements from
        // every total below — the money wasn't lost, but it vanished from the Owner's own earnings
        // dashboard, which is exactly the kind of number an Owner would notice and distrust.
        var pending = settlements
            .Where(s => s.Status is SettlementStatus.Scheduled or SettlementStatus.PendingReview)
            .ToList();
        var completed = settlements
            .Where(s => s.Status == SettlementStatus.Released)
            .ToList();

        var recent = settlements
            .OrderByDescending(s => s.Id)
            .Take(10)
            .Select(s => new RecentSettlementDto(
                s.Id,
                s.NetAmount,
                s.Status.ToString(),
                s.ScheduledAt,
                s.ReleasedAt))
            .ToList();

        return new EarningsSummaryDto(
            TotalEarned: completed.Sum(s => s.NetAmount) + pending.Sum(s => s.NetAmount),
            PendingSettlement: pending.Sum(s => s.NetAmount),
            CompletedSettlement: completed.Sum(s => s.NetAmount),
            PendingSettlementCount: pending.Count,
            RecentSettlements: recent);
    }
}
