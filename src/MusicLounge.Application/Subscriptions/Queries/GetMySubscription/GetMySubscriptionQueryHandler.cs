using MediatR;
using MusicLounge.Application.Common.Interfaces;
using MusicLounge.Application.Subscriptions.DTOs;
using MusicLounge.Domain.Entities;

namespace MusicLounge.Application.Subscriptions.Queries.GetMySubscription;

internal sealed class GetMySubscriptionQueryHandler
    : IRequestHandler<GetMySubscriptionQuery, MySubscriptionDto?>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMySubscriptionQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<MySubscriptionDto?> Handle(GetMySubscriptionQuery request, CancellationToken ct)
    {
        var subs = await _uow.Repository<OwnerSubscription, int>().FindAsync(
            s => s.OwnerId == _currentUser.UserId, ct);

        var latest = subs.OrderByDescending(s => s.StartedAt).FirstOrDefault();
        if (latest is null) return null;

        var package = await _uow.Repository<SubscriptionPackage, int>().GetByIdAsync(latest.PackageId, ct);

        return new MySubscriptionDto(
            latest.Id, latest.PackageId, package?.Name ?? string.Empty,
            latest.StartedAt, latest.ExpiresAt, latest.Status.ToString(),
            latest.MaxTicketsPerEventSnapshot, latest.HasAiPosterSnapshot, latest.MaxAiPostersPerMonthSnapshot,
            latest.MaxTourScenesSnapshot);
    }
}
