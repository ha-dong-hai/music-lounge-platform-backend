using MediatR;
using MusicLounge.Application.Common;
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

        // MLACP-483: dem theo DUNG luat ma lenh tao poster dung, khong chep lai. Neu hai noi troi ra khoi nhau thi man
        // hinh bao "con 3" trong khi may chu tu choi vi da het — nguoi dung khong co cach nao hieu chuyen gi xay ra.
        var dauThang = AiPosterQuota.DauThang(DateTimeOffset.UtcNow);
        var cacLuot = await _uow.Repository<AiPosterGeneration, int>()
            .FindAsync(g => g.OwnerId == _currentUser.UserId, ct);
        var daDung = cacLuot.Count(g => g.CreatedAt >= dauThang && AiPosterQuota.ChiemMotSuat(g.Status));

        return new MySubscriptionDto(
            latest.Id, latest.PackageId, package?.Name ?? string.Empty,
            latest.StartedAt, latest.ExpiresAt, latest.Status.ToString(),
            latest.MaxTicketsPerEventSnapshot, latest.HasAiPosterSnapshot, latest.MaxAiPostersPerMonthSnapshot,
            daDung, AiPosterQuota.ConLai(latest.MaxAiPostersPerMonthSnapshot, daDung),
            latest.MaxTourScenesSnapshot, latest.CancelledAt);
    }
}
